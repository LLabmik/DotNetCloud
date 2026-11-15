using DotNetCloud.Core;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Resolves the effective <see cref="ModuleStoreOptions"/> for a store operation: the
/// configuration-bound values (<c>Modules:Store</c>) with the administrator's
/// <c>dotnetcloud.core</c>/<see cref="SystemSettingKeys.ModuleStoreIndexUrl"/> override layered on top.
/// </summary>
/// <remarks>
/// <para>
/// The admin settings page writes the store URL to the core <c>SystemSettings</c> table. The store
/// client is built once at startup, so it cannot read that row by itself — this provider is the
/// single source of truth both the client factory and the CLI consult. The result is cached for
/// <see cref="CacheDuration"/> so a catalogue read or an install costs at most one settings query.
/// </para>
/// <para>
/// Trust is deliberately not part of the override: signing keys stay in configuration
/// (<c>Modules:Store:TrustedKeys</c>), because a compromised store page must never be able to
/// change which keys are trusted.
/// </para>
/// </remarks>
public sealed class ModuleStoreSettingsProvider
{
    /// <summary>How long an effective-options snapshot is reused before the settings row is read again.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IDbContextFactory _dbContextFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<ModuleStoreSettingsProvider> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private ModuleStoreOptions? _cached;
    private DateTimeOffset _cachedUntilUtc;

    /// <summary>Initializes a new instance of the <see cref="ModuleStoreSettingsProvider"/> class.</summary>
    /// <param name="dbContextFactory">Factory used for the short-lived settings query.</param>
    /// <param name="timeProvider">Clock used for cache expiry.</param>
    /// <param name="logger">Logger.</param>
    public ModuleStoreSettingsProvider(
        IDbContextFactory dbContextFactory,
        TimeProvider timeProvider,
        ILogger<ModuleStoreSettingsProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _time = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns the options a store client should use right now: <paramref name="configured"/> with
    /// the admin store URL applied when one is set and usable.
    /// </summary>
    /// <param name="configured">The configuration-bound options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective options for this operation.</returns>
    public async Task<ModuleStoreOptions> GetEffectiveOptionsAsync(
        ModuleStoreOptions configured,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configured);

        if (_cached is { } cached && _time.GetUtcNow() < _cachedUntilUtc)
        {
            return cached;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is { } current && _time.GetUtcNow() < _cachedUntilUtc)
            {
                return current;
            }

            var overrideUrl = await ReadOverrideAsync(cancellationToken).ConfigureAwait(false);

            var effective = configured.Clone();
            if (overrideUrl is not null)
            {
                effective.IndexUrl = overrideUrl;
            }

            _cached = effective;
            _cachedUntilUtc = _time.GetUtcNow() + CacheDuration;
            return effective;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>
    /// Reads the admin store URL. Returns <see langword="null"/> when no usable override is stored,
    /// so the caller keeps the configured value.
    /// </summary>
    private async Task<string?> ReadOverrideAsync(CancellationToken cancellationToken)
    {
        string? value;
        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();

            value = await dbContext.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.Module == SystemSettingKeys.CoreModule
                    && setting.Key == SystemSettingKeys.ModuleStoreIndexUrl)
                .Select(setting => setting.Value)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database being unavailable must not take the store page down: fall back to the
            // configured value until the cache expires and the next read is attempted.
            _logger.LogDebug("Could not read the admin module store URL: {Error}", LogSanitizer.Sanitize(ex.Message));
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!ModuleStoreUrls.IsUsable(trimmed))
        {
            _logger.LogWarning(
                "The admin module store URL is not a usable https index URL; the configured Modules:Store:IndexUrl is used instead.");
            return null;
        }

        return trimmed;
    }
}
