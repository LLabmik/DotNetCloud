using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Files;

/// <summary>
/// <see cref="IWopiRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;IWopiRestClient, HttpWopiRestClient&gt;()</c>, so the shared
/// authenticated handler attaches (and refreshes) the bearer token.
/// </summary>
internal sealed class HttpWopiRestClient : IWopiRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpWopiRestClient> _logger;

    /// <summary>Initializes a new <see cref="HttpWopiRestClient"/>.</summary>
    public HttpWopiRestClient(HttpClient http, ILogger<HttpWopiRestClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WopiDiscovery> GetDiscoveryAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        try
        {
            SetAuth(accessToken);
            var dto = await GetEnvelopeDataAsync<DiscoveryDto>(
                $"{Url(serverBaseUrl)}/api/v1/wopi/discovery", ct).ConfigureAwait(false);

            if (dto is null || !dto.Available)
                return WopiDiscovery.Unavailable;

            var extensions = (dto.SupportedExtensions ?? [])
                .Select(CollaboraEditability.NormalizeExtension)
                .Where(ext => ext.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WopiDiscovery(true, extensions);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogDebug(ex, "WOPI discovery failed; Collabora document editing is unavailable.");
            return WopiDiscovery.Unavailable;
        }
    }

    /// <inheritdoc />
    public async Task<WopiEditorSession> OpenEditorAsync(
        string serverBaseUrl, string accessToken, Guid nodeId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsync(
            $"{Url(serverBaseUrl)}/api/v1/wopi/token/{nodeId}", null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var dto = await ReadEnvelopeDataAsync<EditorSessionDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned an empty WOPI token response.");

        if (string.IsNullOrWhiteSpace(dto.EditorUrl))
            throw new InvalidOperationException("Server returned no Collabora editor URL.");

        return new WopiEditorSession(
            dto.AccessToken ?? string.Empty,
            dto.AccessTokenTtl,
            dto.WopiSrc ?? string.Empty,
            dto.EditorUrl);
    }

    /// <inheritdoc />
    public async Task CloseEditorAsync(
        string serverBaseUrl, string accessToken, Guid nodeId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/wopi/token/{nodeId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string Url(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body, unwrapping the server's standard envelope
    /// (<c>{"success":true,"data":...}</c>) if present. WOPI token/discovery routes are enveloped;
    /// raw WOPI protocol routes are not — both shapes are tolerated.
    /// </summary>
    private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }

    // ── DTOs ─────────────────────────────────────────────────────────

    private sealed class DiscoveryDto
    {
        public bool Available { get; init; }
        public IReadOnlyList<string>? SupportedExtensions { get; init; }
    }

    private sealed class EditorSessionDto
    {
        public string? AccessToken { get; init; }
        public long AccessTokenTtl { get; init; }
        public string? WopiSrc { get; init; }
        public string? EditorUrl { get; init; }
    }
}
