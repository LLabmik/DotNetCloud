using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Maps the admin-editable <c>VersionRetention:*</c> settings (SystemSettings, module
/// <c>dotnetcloud.files</c>) onto <see cref="VersionRetentionOptions"/>.
/// </summary>
/// <remarks>
/// The <c>/admin/files</c> page writes these rows; they are layered over the configuration-bound
/// baseline so the page shows and controls what is actually enforced.
/// </remarks>
public static class FileVersioningAdminSettings
{
    /// <summary>Module that owns the file versioning admin settings.</summary>
    public const string ModuleId = "dotnetcloud.files";

    /// <summary>Setting key for the versioning master switch.</summary>
    public const string EnabledKey = "VersionRetention:Enabled";

    /// <summary>Setting key for the maximum number of versions kept per file.</summary>
    public const string MaxNumberKey = "VersionRetention:MaxNumber";

    /// <summary>Setting key for the maximum age (days) of a retained version.</summary>
    public const string MaxDaysKey = "VersionRetention:MaxDays";

    /// <summary>Setting keys the overlay understands (all optional; absent keys keep the configured value).</summary>
    public static readonly IReadOnlyList<string> Keys = [EnabledKey, MaxNumberKey, MaxDaysKey];

    /// <summary>
    /// The number of versions actually retained per file. Disabling versioning means "keep the
    /// current version only", which is expressed as a limit of one — the newest version is always
    /// protected, so the file keeps working while its history is released.
    /// </summary>
    /// <param name="options">Effective options.</param>
    public static int EffectiveMaxVersionCount(VersionRetentionOptions options)
        => options.Enabled ? options.MaxVersionCount : 1;

    /// <summary>Returns a copy of <paramref name="source"/> so overlays never mutate the shared instance.</summary>
    /// <param name="source">Options to copy.</param>
    public static VersionRetentionOptions Copy(VersionRetentionOptions source) => new()
    {
        Enabled = source.Enabled,
        MaxVersionCount = source.MaxVersionCount,
        RetentionDays = source.RetentionDays,
        CleanupInterval = source.CleanupInterval
    };

    /// <summary>
    /// Applies every present admin value onto <paramref name="options"/>, leaving keys that have no
    /// row untouched (so configuration keeps working as the baseline).
    /// </summary>
    /// <param name="options">Options to update in place.</param>
    /// <param name="values">Stored values keyed by setting name.</param>
    public static void Apply(VersionRetentionOptions options, IReadOnlyDictionary<string, string> values)
    {
        string? Text(string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        bool? Flag(string key) =>
            bool.TryParse(Text(key), out var parsed) ? parsed : null;

        int? Count(string key) =>
            int.TryParse(Text(key), out var parsed) && parsed >= 0 ? parsed : null;

        options.Enabled = Flag(EnabledKey) ?? options.Enabled;
        options.MaxVersionCount = Count(MaxNumberKey) ?? options.MaxVersionCount;
        options.RetentionDays = Count(MaxDaysKey) ?? options.RetentionDays;
    }
}

/// <summary>
/// Default <see cref="IFileVersioningSettingsProvider"/>: reads the admin settings rows for
/// <c>dotnetcloud.files</c> and overlays them onto the bound configuration.
/// </summary>
/// <remarks>
/// The values are cached briefly so a burst of uploads does not issue one settings read each, while
/// an admin edit is still picked up promptly. Each process reads the database itself, so the
/// Core.Server in-process container and the Files module host converge without any IPC.
/// </remarks>
public sealed class FileVersioningSettingsProvider : IFileVersioningSettingsProvider
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound for the synchronous <see cref="Current"/> read. It must not be unbounded: blocking a
    /// ThreadPool thread on the async gate starves the pool, the gate holder's continuation never runs,
    /// and every caller waits forever (the Files page used to hang exactly this way).
    /// </summary>
    private static readonly TimeSpan DefaultBlockingRefreshTimeout = TimeSpan.FromSeconds(2);

    private readonly IOptions<VersionRetentionOptions> _baseline;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileVersioningSettingsProvider> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _blockingRefreshTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private VersionRetentionOptions? _cached;
    private DateTime _cachedAtUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileVersioningSettingsProvider"/> class.
    /// </summary>
    /// <param name="baseline">Configuration-bound options used as the baseline.</param>
    /// <param name="scopeFactory">Used to resolve <see cref="IAdminSettingsService"/> from a scope.</param>
    /// <param name="logger">Logger instance.</param>
    public FileVersioningSettingsProvider(
        IOptions<VersionRetentionOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<FileVersioningSettingsProvider> logger)
        : this(baseline, scopeFactory, logger, DefaultCacheTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileVersioningSettingsProvider"/> class with an explicit cache window.
    /// </summary>
    /// <param name="baseline">Configuration-bound options used as the baseline.</param>
    /// <param name="scopeFactory">Used to resolve <see cref="IAdminSettingsService"/> from a scope.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cacheTtl">How long resolved values are cached; <see cref="TimeSpan.Zero"/> disables caching.</param>
    /// <param name="blockingRefreshTimeout">
    /// Upper bound for the synchronous <see cref="Current"/> read; the last known value is used when it
    /// elapses. Must stay small — the read runs on a caller that cannot await.
    /// </param>
    public FileVersioningSettingsProvider(
        IOptions<VersionRetentionOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<FileVersioningSettingsProvider> logger,
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
    public VersionRetentionOptions Current
    {
        get
        {
            if (!IsExpired())
            {
                return _cached!;
            }

            // Callers that cannot await need a synchronous read, but it has to be bounded so it can
            // never starve the ThreadPool and wedge the process.
            using var timeout = new CancellationTokenSource(_blockingRefreshTimeout);
            try
            {
                return GetAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Refreshing the version retention settings took longer than {Timeout}; using the last known values.",
                    _blockingRefreshTimeout);
                return _cached ?? _baseline.Value;
            }
        }
    }

    /// <inheritdoc />
    public async Task<VersionRetentionOptions> GetAsync(CancellationToken cancellationToken = default)
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

    private async Task<VersionRetentionOptions> LoadAsync(CancellationToken cancellationToken)
    {
        // Always start from a fresh copy: the baseline instance is shared with every other consumer.
        var effective = FileVersioningAdminSettings.Copy(_baseline.Value);

        try
        {
            var values = await ReadAdminValuesAsync(cancellationToken).ConfigureAwait(false);
            if (values.Count > 0)
            {
                FileVersioningAdminSettings.Apply(effective, values);
            }
        }
        catch (Exception ex)
        {
            // A settings read must never break uploads; fall back to configuration.
            _logger.LogWarning(ex,
                "Could not read the file versioning admin settings; using the configured values.");
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
                "No IAdminSettingsService in this process; file versioning uses the configured Files:VersionRetention values.");
            return [];
        }

        var rows = await service.ListSettingsAsync(FileVersioningAdminSettings.ModuleId).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            // ListSettingsAsync filters with a substring match, so re-check module equality.
            if (string.Equals(row.Module, FileVersioningAdminSettings.ModuleId, StringComparison.OrdinalIgnoreCase))
            {
                map[row.Key] = row.Value;
            }
        }

        return map;
    }
}
