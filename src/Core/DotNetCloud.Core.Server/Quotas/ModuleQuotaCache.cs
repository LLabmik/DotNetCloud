using System.Collections.Concurrent;

namespace DotNetCloud.Core.Server.Quotas;

/// <summary>
/// Short-lived cache of effective module quotas so rate limiting does not query the database on
/// every capability call.
/// </summary>
/// <remarks>
/// The cache is refreshed lazily after a fixed TTL (60 seconds by default). An admin quota change
/// therefore takes up to one TTL to become visible to the limiter, which is acceptable for a
/// safety control and keeps the hot path free of database I/O.
/// </remarks>
internal sealed class ModuleQuotaCache
{
    /// <summary>Default lifetime of a cached quota.</summary>
    internal static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ModuleQuotaResolver _resolver;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;

    /// <summary>Initializes a new instance of the <see cref="ModuleQuotaCache"/> class.</summary>
    /// <param name="resolver">Resolver used to compute the effective quota on a cache miss.</param>
    /// <param name="timeProvider">Clock used for TTL evaluation.</param>
    public ModuleQuotaCache(ModuleQuotaResolver resolver, TimeProvider timeProvider)
        : this(resolver, timeProvider, DefaultTtl)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ModuleQuotaCache"/> class with an explicit TTL.</summary>
    /// <param name="resolver">Resolver used to compute the effective quota on a cache miss.</param>
    /// <param name="timeProvider">Clock used for TTL evaluation.</param>
    /// <param name="ttl">How long a resolved quota stays cached.</param>
    internal ModuleQuotaCache(ModuleQuotaResolver resolver, TimeProvider timeProvider, TimeSpan ttl)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _ttl = ttl;
    }

    /// <summary>Gets the effective quota for a module, resolving it when the cached value expired.</summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective quota.</returns>
    public async Task<EffectiveModuleQuota> GetAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var now = _timeProvider.GetUtcNow();
        if (_entries.TryGetValue(moduleId, out var entry) && entry.ExpiresAtUtc > now)
        {
            return entry.Quota;
        }

        var quota = await _resolver.ResolveAsync(moduleId, new ModuleQuotaRequest(), cancellationToken);
        _entries[moduleId] = new Entry(quota, now + _ttl);
        return quota;
    }

    /// <summary>Drops the cached quota for a module (used when an admin changes the quota).</summary>
    /// <param name="moduleId">The module identifier.</param>
    public void Invalidate(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        _entries.TryRemove(moduleId, out _);
    }

    private sealed record Entry(EffectiveModuleQuota Quota, DateTimeOffset ExpiresAtUtc);
}
