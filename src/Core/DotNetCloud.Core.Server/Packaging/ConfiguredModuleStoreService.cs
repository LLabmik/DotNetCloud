using DotNetCloud.Core.Modules.Store;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// An <see cref="IModuleStoreService"/> that re-resolves the configured store source at operation
/// time, so an administrator can change the store index URL from the admin settings page without
/// restarting the server.
/// </summary>
/// <remarks>
/// The underlying remote/local client is rebuilt only when the effective source changes; otherwise
/// the same instance is reused, which keeps its in-memory index and download caches warm. The
/// effective source comes from <see cref="ModuleStoreSettingsProvider"/> (configuration with the
/// admin override on top), so trust keys and every other option still come from configuration.
/// </remarks>
public sealed class ConfiguredModuleStoreService : IModuleStoreService
{
    private readonly Func<ModuleStoreOptions, IModuleStoreService> _build;
    private readonly ModuleStoreSettingsProvider _settings;
    private readonly ModuleStoreOptions _configured;
    private readonly object _sync = new();

    private IModuleStoreService _current;
    private string _currentKey;

    /// <summary>Initializes a new instance of the <see cref="ConfiguredModuleStoreService"/> class.</summary>
    /// <param name="build">Builds the concrete client for a given set of effective options.</param>
    /// <param name="settings">Provider of the effective options (configuration plus admin override).</param>
    /// <param name="configured">The configuration-bound options, used until the first refresh.</param>
    public ConfiguredModuleStoreService(
        Func<ModuleStoreOptions, IModuleStoreService> build,
        ModuleStoreSettingsProvider settings,
        ModuleStoreOptions configured)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(configured);

        _build = build;
        _settings = settings;
        _configured = configured;
        _current = build(configured);
        _currentKey = SourceKey(configured);
    }

    /// <inheritdoc/>
    public string SourceId => Volatile.Read(ref _current).SourceId;

    /// <inheritdoc/>
    public ModuleStoreSourceKind SourceKind => Volatile.Read(ref _current).SourceKind;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleStorePackage>> ListPackagesAsync(CancellationToken cancellationToken = default)
    {
        var store = await GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await store.ListPackagesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ModuleStoreResolution?> ResolveAsync(
        string moduleId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var store = await GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await store.ResolveAsync(moduleId, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rebuilds the underlying client when the effective source changed since the last operation.
    /// </summary>
    private async Task<IModuleStoreService> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var effective = await _settings.GetEffectiveOptionsAsync(_configured, cancellationToken).ConfigureAwait(false);
        var key = SourceKey(effective);

        var current = Volatile.Read(ref _current);
        if (string.Equals(key, _currentKey, StringComparison.Ordinal))
        {
            return current;
        }

        lock (_sync)
        {
            if (string.Equals(key, _currentKey, StringComparison.Ordinal))
            {
                return _current;
            }

            _current = _build(effective);
            _currentKey = key;
            return _current;
        }
    }

    private static string SourceKey(ModuleStoreOptions options) =>
        !string.IsNullOrWhiteSpace(options.IndexUrl)
            ? "remote|" + options.IndexUrl.Trim()
            : "local|" + (options.LocalFeedPath ?? string.Empty);
}
