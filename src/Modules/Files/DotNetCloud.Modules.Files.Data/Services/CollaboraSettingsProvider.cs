using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Maps the admin-editable <c>Collabora:*</c> settings (SystemSettings, module
/// <c>dotnetcloud.files</c>) onto <see cref="CollaboraOptions"/>.
/// </summary>
/// <remarks>
/// Before this existed the <c>/admin/collabora</c> page wrote those rows while every Collabora code
/// path read configuration only — so the page could neither show the effective values nor change
/// them. The overlay makes the page authoritative.
/// </remarks>
public static class CollaboraAdminSettings
{
    /// <summary>Module that owns the Collabora admin settings.</summary>
    public const string ModuleId = "dotnetcloud.files";

    /// <summary>Setting keys the overlay understands (all optional; absent keys keep the configured value).</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "Collabora:Enabled",
        "Collabora:ServerUrl",
        "Collabora:DiscoveryUrl",
        "Collabora:WopiBaseUrl",
        "Collabora:TokenSigningKey",
        "Collabora:TokenLifetimeMinutes",
        "Collabora:EnableProofKeyValidation",
        "Collabora:AutoSaveIntervalSeconds",
        "Collabora:MaxConcurrentSessions",
        "Collabora:SupportedMimeTypes",
        "Collabora:UseBuiltInCollabora",
        "Collabora:AllowInsecureTls",
        "Collabora:CollaboraInstallDirectory",
        "Collabora:CollaboraExecutablePath",
        "Collabora:CollaboraMaxRestartAttempts",
        "Collabora:CollaboraRestartBackoffSeconds"
    ];

    /// <summary>Returns a deep copy of <paramref name="source"/> so overlays never mutate the shared instance.</summary>
    /// <param name="source">Options to copy.</param>
    public static CollaboraOptions Copy(CollaboraOptions source) => new()
    {
        ServerUrl = source.ServerUrl,
        DiscoveryUrl = source.DiscoveryUrl,
        Enabled = source.Enabled,
        AutoSaveIntervalSeconds = source.AutoSaveIntervalSeconds,
        MaxConcurrentSessions = source.MaxConcurrentSessions,
        TokenLifetimeMinutes = source.TokenLifetimeMinutes,
        TokenSigningKey = source.TokenSigningKey,
        WopiBaseUrl = source.WopiBaseUrl,
        EnableProofKeyValidation = source.EnableProofKeyValidation,
        AllowInsecureTls = source.AllowInsecureTls,
        SupportedMimeTypes = [.. source.SupportedMimeTypes],
        UseBuiltInCollabora = source.UseBuiltInCollabora,
        CollaboraInstallDirectory = source.CollaboraInstallDirectory,
        CollaboraExecutablePath = source.CollaboraExecutablePath,
        CollaboraMaxRestartAttempts = source.CollaboraMaxRestartAttempts,
        CollaboraRestartBackoffSeconds = source.CollaboraRestartBackoffSeconds
    };

    /// <summary>
    /// Applies every present admin value onto <paramref name="options"/>, leaving keys that have no
    /// row untouched (so configuration keeps working as the baseline).
    /// </summary>
    /// <param name="options">Options to update in place.</param>
    /// <param name="values">Stored values keyed by setting name.</param>
    public static void Apply(CollaboraOptions options, IReadOnlyDictionary<string, string> values)
    {
        string? Text(string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        bool? Flag(string key) =>
            bool.TryParse(Text(key), out var parsed) ? parsed : null;

        int? Number(string key) =>
            int.TryParse(Text(key), out var parsed) ? parsed : null;

        options.Enabled = Flag("Collabora:Enabled") ?? options.Enabled;
        options.ServerUrl = Text("Collabora:ServerUrl") ?? options.ServerUrl;
        options.DiscoveryUrl = Text("Collabora:DiscoveryUrl") ?? options.DiscoveryUrl;
        options.WopiBaseUrl = Text("Collabora:WopiBaseUrl") ?? options.WopiBaseUrl;
        options.TokenSigningKey = Text("Collabora:TokenSigningKey") ?? options.TokenSigningKey;
        options.TokenLifetimeMinutes = Number("Collabora:TokenLifetimeMinutes") ?? options.TokenLifetimeMinutes;
        options.EnableProofKeyValidation = Flag("Collabora:EnableProofKeyValidation") ?? options.EnableProofKeyValidation;
        options.AutoSaveIntervalSeconds = Number("Collabora:AutoSaveIntervalSeconds") ?? options.AutoSaveIntervalSeconds;
        options.MaxConcurrentSessions = Number("Collabora:MaxConcurrentSessions") ?? options.MaxConcurrentSessions;
        options.UseBuiltInCollabora = Flag("Collabora:UseBuiltInCollabora") ?? options.UseBuiltInCollabora;
        options.AllowInsecureTls = Flag("Collabora:AllowInsecureTls") ?? options.AllowInsecureTls;
        options.CollaboraInstallDirectory = Text("Collabora:CollaboraInstallDirectory") ?? options.CollaboraInstallDirectory;
        options.CollaboraExecutablePath = Text("Collabora:CollaboraExecutablePath") ?? options.CollaboraExecutablePath;
        options.CollaboraMaxRestartAttempts = Number("Collabora:CollaboraMaxRestartAttempts") ?? options.CollaboraMaxRestartAttempts;
        options.CollaboraRestartBackoffSeconds =
            Number("Collabora:CollaboraRestartBackoffSeconds") ?? options.CollaboraRestartBackoffSeconds;

        // An empty value means "inherit from discovery" rather than "restrict to nothing".
        if (Text("Collabora:SupportedMimeTypes") is { } mimeTypes)
        {
            options.SupportedMimeTypes = [.. mimeTypes
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }
    }
}

/// <summary>
/// Default <see cref="ICollaboraSettingsProvider"/>: reads the admin settings rows for
/// <c>dotnetcloud.files</c> and overlays them onto the bound configuration.
/// </summary>
/// <remarks>
/// The values are cached briefly so a burst of WOPI requests does not issue one settings read each,
/// while an admin edit is still picked up promptly. Each process reads the database itself, so the
/// Core.Server in-process container and the Files module host converge without any IPC.
/// </remarks>
public sealed class CollaboraSettingsProvider : ICollaboraSettingsProvider
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound for the synchronous <see cref="Current"/> read. It must not be unbounded: blocking a
    /// ThreadPool thread on the async gate starves the pool, the gate holder's continuation never runs,
    /// and every caller waits forever (this is how the Files page used to hang until a restart).
    /// </summary>
    private static readonly TimeSpan DefaultBlockingRefreshTimeout = TimeSpan.FromSeconds(2);

    private readonly IOptions<CollaboraOptions> _baseline;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CollaboraSettingsProvider> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _blockingRefreshTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CollaboraOptions? _cached;
    private DateTime _cachedAtUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaboraSettingsProvider"/> class.
    /// </summary>
    /// <param name="baseline">Configuration-bound options used as the baseline.</param>
    /// <param name="scopeFactory">Used to resolve <see cref="IAdminSettingsService"/> from a scope.</param>
    /// <param name="logger">Logger instance.</param>
    public CollaboraSettingsProvider(
        IOptions<CollaboraOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<CollaboraSettingsProvider> logger)
        : this(baseline, scopeFactory, logger, DefaultCacheTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollaboraSettingsProvider"/> class with an explicit cache window.
    /// </summary>
    /// <param name="baseline">Configuration-bound options used as the baseline.</param>
    /// <param name="scopeFactory">Used to resolve <see cref="IAdminSettingsService"/> from a scope.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cacheTtl">How long resolved values are cached; <see cref="TimeSpan.Zero"/> disables caching.</param>
    /// <param name="blockingRefreshTimeout">
    /// Upper bound for the synchronous <see cref="Current"/> read; the last known value is used when it
    /// elapses. Must stay small — the read runs on a caller that cannot await.
    /// </param>
    internal CollaboraSettingsProvider(
        IOptions<CollaboraOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<CollaboraSettingsProvider> logger,
        TimeSpan cacheTtl,
        TimeSpan? blockingRefreshTimeout = null)
    {
        _baseline = baseline;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cacheTtl = cacheTtl;
        _blockingRefreshTimeout = blockingRefreshTimeout ?? DefaultBlockingRefreshTimeout;
    }

    /// <inheritdoc />
    public CollaboraOptions Current
    {
        get
        {
            if (!IsExpired())
            {
                return _cached!;
            }

            // Callers that cannot await (DI handler factories, WOPI plumbing) need a synchronous read,
            // but it has to be bounded so it can never starve the ThreadPool and wedge the process.
            using var timeout = new CancellationTokenSource(_blockingRefreshTimeout);
            try
            {
                return GetAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Refreshing the Collabora settings took longer than {Timeout}; using the last known values.",
                    _blockingRefreshTimeout);
                return _cached ?? _baseline.Value;
            }
        }
    }

    /// <inheritdoc />
    public async Task<CollaboraOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!IsExpired())
        {
            return _cached!;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsExpired())
            {
                return _cached!;
            }

            var resolved = await LoadAsync(cancellationToken).ConfigureAwait(false);

            // LoadAsync degrades to the configuration baseline when it is cancelled (its catch swallows
            // the cancellation), so a timed-out read must not be cached — that would hide the
            // administrator's values until the cache window elapses again.
            if (cancellationToken.IsCancellationRequested)
            {
                return _cached ?? resolved;
            }

            _cached = resolved;
            _cachedAtUtc = DateTime.UtcNow;
            return resolved;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        _cached = null;
        _cachedAtUtc = default;
    }

    private bool IsExpired() => _cached is null || _cacheTtl <= TimeSpan.Zero || DateTime.UtcNow - _cachedAtUtc >= _cacheTtl;

    private async Task<CollaboraOptions> LoadAsync(CancellationToken cancellationToken)
    {
        // Always start from a fresh copy: the baseline instance is shared with every other consumer.
        var effective = CollaboraAdminSettings.Copy(_baseline.Value);

        try
        {
            var values = await ReadAdminValuesAsync(cancellationToken).ConfigureAwait(false);
            if (values.Count > 0)
            {
                CollaboraAdminSettings.Apply(effective, values);
            }
        }
        catch (Exception ex)
        {
            // A settings read must never take document editing down; fall back to configuration.
            _logger.LogWarning(ex,
                "Could not read the Collabora admin settings; using the configured values.");
        }

        return effective;
    }

    private async Task<Dictionary<string, string>> ReadAdminValuesAsync(CancellationToken cancellationToken)
    {
        // This provider is a singleton, so the (scoped) settings service is resolved from a scope of
        // its own: resolving scoped services from the root provider fails under scope validation and
        // would silently reduce the page to configuration values.
        using var scope = _scopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService(typeof(IAdminSettingsService)) is not IAdminSettingsService service)
        {
            // Module hosts that only piggyback the Files background services (music, photos, video) do
            // not register the settings reader; they keep using the configured baseline.
            _logger.LogDebug(
                "No IAdminSettingsService in this process; Collabora uses the configured Files:Collabora values.");
            return [];
        }

        var rows = await service.ListSettingsAsync(CollaboraAdminSettings.ModuleId).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            // ListSettingsAsync filters with a substring match, so re-check module equality.
            if (string.Equals(row.Module, CollaboraAdminSettings.ModuleId, StringComparison.OrdinalIgnoreCase))
            {
                map[row.Key] = row.Value;
            }
        }

        return map;
    }
}
