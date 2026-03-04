using System.Xml.Linq;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Discovers Collabora Online capabilities by fetching and parsing the WOPI discovery XML.
/// Caches results to avoid repeated HTTP calls.
/// </summary>
internal sealed class CollaboraDiscoveryService : ICollaboraDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CollaboraOptions _options;
    private readonly ILogger<CollaboraDiscoveryService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CollaboraDiscoveryResult? _cachedResult;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public CollaboraDiscoveryService(
        IHttpClientFactory httpClientFactory,
        IOptions<CollaboraOptions> options,
        ILogger<CollaboraDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CollaboraDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedResult is not null && DateTime.UtcNow < _cacheExpiry)
            return _cachedResult;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedResult is not null && DateTime.UtcNow < _cacheExpiry)
                return _cachedResult;

            if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            {
                return new CollaboraDiscoveryResult { IsAvailable = false };
            }

            var discoveryUrl = $"{_options.ServerUrl.TrimEnd('/')}/hosting/discovery";

            using var client = _httpClientFactory.CreateClient("Collabora");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetAsync(discoveryUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Collabora discovery failed: HTTP {StatusCode} from {Url}",
                    (int)response.StatusCode, discoveryUrl);
                return new CollaboraDiscoveryResult { IsAvailable = false };
            }

            var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = ParseDiscoveryXml(xmlContent);

            _cachedResult = result;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            _logger.LogInformation("Collabora discovery successful: {ActionCount} actions available", result.Actions.Count);

            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Collabora discovery failed: cannot connect to {Url}", _options.ServerUrl);
            return new CollaboraDiscoveryResult { IsAvailable = false };
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetEditorUrlAsync(string extension, string action = "edit", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var discovery = await DiscoverAsync(cancellationToken);
        if (!discovery.IsAvailable)
            return null;

        var normalizedExt = extension.TrimStart('.').ToLowerInvariant();

        var match = discovery.Actions.FirstOrDefault(a =>
            string.Equals(a.Extension, normalizedExt, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Action, action, StringComparison.OrdinalIgnoreCase));

        // Fall back to "view" if "edit" is not available
        if (match is null && !string.Equals(action, "view", StringComparison.OrdinalIgnoreCase))
        {
            match = discovery.Actions.FirstOrDefault(a =>
                string.Equals(a.Extension, normalizedExt, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.Action, "view", StringComparison.OrdinalIgnoreCase));
        }

        return match?.UrlSrc;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ServerUrl))
            return false;

        var discovery = await DiscoverAsync(cancellationToken);
        return discovery.IsAvailable;
    }

    /// <inheritdoc />
    public async Task<bool> IsSupportedExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var discovery = await DiscoverAsync(cancellationToken);
        if (!discovery.IsAvailable)
            return false;

        var normalizedExt = extension.TrimStart('.').ToLowerInvariant();

        // If a MIME type allow-list is configured, only allow extensions whose MIME type is listed
        if (_options.SupportedMimeTypes.Count > 0)
        {
            return discovery.Actions.Any(a =>
                string.Equals(a.Extension, normalizedExt, StringComparison.OrdinalIgnoreCase) &&
                a.MimeType is not null &&
                _options.SupportedMimeTypes.Contains(a.MimeType, StringComparer.OrdinalIgnoreCase));
        }

        return discovery.Actions.Any(a =>
            string.Equals(a.Extension, normalizedExt, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Parses the Collabora WOPI discovery XML document.
    /// </summary>
    /// <remarks>
    /// Collabora discovery XML format:
    /// <code>
    /// &lt;wopi-discovery&gt;
    ///   &lt;net-zone name="external-http"&gt;
    ///     &lt;app name="writer"&gt;
    ///       &lt;action name="edit" ext="docx" urlsrc="https://..." /&gt;
    ///     &lt;/app&gt;
    ///   &lt;/net-zone&gt;
    ///   &lt;proof-key modulus="..." exponent="..." oldmodulus="..." oldexponent="..." /&gt;
    /// &lt;/wopi-discovery&gt;
    /// </code>
    /// </remarks>
    internal static CollaboraDiscoveryResult ParseDiscoveryXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var actions = new List<CollaboraAppAction>();

        string? proofKey = null;
        string? oldProofKey = null;
        string? proofKeyValue = null;
        string? oldProofKeyValue = null;

        // Parse proof-key element
        var proofKeyElement = doc.Root?.Element("proof-key");
        if (proofKeyElement is not null)
        {
            // Modulus attributes (legacy/fallback)
            proofKey = proofKeyElement.Attribute("modulus")?.Value;
            oldProofKey = proofKeyElement.Attribute("oldmodulus")?.Value;

            // SubjectPublicKeyInfo values — used for RSA-SHA256 proof verification
            proofKeyValue = proofKeyElement.Attribute("value")?.Value;
            oldProofKeyValue = proofKeyElement.Attribute("old-value")?.Value;
        }

        // Parse net-zone/app/action elements
        var netZones = doc.Root?.Elements("net-zone") ?? [];
        foreach (var zone in netZones)
        {
            var apps = zone.Elements("app");
            foreach (var app in apps)
            {
                var appName = app.Attribute("name")?.Value ?? string.Empty;

                foreach (var action in app.Elements("action"))
                {
                    var actionName = action.Attribute("name")?.Value;
                    var ext = action.Attribute("ext")?.Value;
                    var urlSrc = action.Attribute("urlsrc")?.Value;

                    if (actionName is not null && ext is not null && urlSrc is not null)
                    {
                        actions.Add(new CollaboraAppAction
                        {
                            AppName = appName,
                            Action = actionName,
                            Extension = ext,
                            MimeType = action.Attribute("mime")?.Value,
                            UrlSrc = urlSrc
                        });
                    }
                }
            }
        }

        return new CollaboraDiscoveryResult
        {
            IsAvailable = actions.Count > 0,
            Actions = actions,
            ProofKey = proofKey,
            OldProofKey = oldProofKey,
            ProofKeyValue = proofKeyValue,
            OldProofKeyValue = oldProofKeyValue,
            FetchedAt = DateTime.UtcNow
        };
    }
}
