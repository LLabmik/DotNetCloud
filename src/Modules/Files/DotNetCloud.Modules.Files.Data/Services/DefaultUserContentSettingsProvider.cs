using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Default <see cref="IDefaultUserContentSettingsProvider"/>: reads the administrator-editable
/// <c>DefaultUserContent:*</c> rows (module <c>dotnetcloud.files</c>) and layers them over the
/// configuration-bound baseline.
/// </summary>
public sealed class DefaultUserContentSettingsProvider : IDefaultUserContentSettingsProvider
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultBlockingRefreshTimeout = TimeSpan.FromSeconds(2);

    private readonly IOptions<DefaultUserContentOptions> _baseline;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DefaultUserContentSettingsProvider> _logger;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _blockingRefreshTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DefaultUserContentOptions? _cached;
    private DateTime _cachedAtUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentSettingsProvider"/> class.
    /// </summary>
    public DefaultUserContentSettingsProvider(
        IOptions<DefaultUserContentOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<DefaultUserContentSettingsProvider> logger)
        : this(baseline, scopeFactory, logger, DefaultCacheTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentSettingsProvider"/> class
    /// with explicit cache controls (used by tests).
    /// </summary>
    public DefaultUserContentSettingsProvider(
        IOptions<DefaultUserContentOptions> baseline,
        IServiceScopeFactory scopeFactory,
        ILogger<DefaultUserContentSettingsProvider> logger,
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
    public DefaultUserContentOptions Current
    {
        get
        {
            if (!IsExpired())
                return _cached!;

            using var timeout = new CancellationTokenSource(_blockingRefreshTimeout);
            try
            {
                return GetAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Refreshing the default-content settings took longer than {Timeout}; using the last known values.",
                    _blockingRefreshTimeout);
                return _cached ?? DefaultUserContentOptions.Copy(_baseline.Value);
            }
        }
    }

    /// <inheritdoc />
    public async Task<DefaultUserContentOptions> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!IsExpired())
            return _cached!;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsExpired())
                return _cached!;

            var resolved = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return _cached ?? resolved;

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

    private bool IsExpired() =>
        _cached is null || _cacheTtl <= TimeSpan.Zero || DateTime.UtcNow - _cachedAtUtc >= _cacheTtl;

    private async Task<DefaultUserContentOptions> LoadAsync(CancellationToken cancellationToken)
    {
        var effective = DefaultUserContentOptions.Copy(_baseline.Value);

        try
        {
            var values = await ReadAdminValuesAsync().ConfigureAwait(false);
            if (values.Count > 0)
                DefaultUserContentOptions.Apply(effective, values);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not read the default-content admin settings; using the configured values.");
        }

        return effective;
    }

    private async Task<Dictionary<string, string>> ReadAdminValuesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService(typeof(IAdminSettingsService)) is not IAdminSettingsService service)
        {
            _logger.LogDebug(
                "No IAdminSettingsService in this process; default content uses the configured Files:DefaultUserContent values.");
            return [];
        }

        var rows = await service.ListSettingsAsync(DefaultUserContentOptions.AdminSettings.ModuleId)
            .ConfigureAwait(false);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.Equals(row.Module, DefaultUserContentOptions.AdminSettings.ModuleId, StringComparison.OrdinalIgnoreCase))
                map[row.Key] = row.Value;
        }

        return map;
    }
}
