using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// EF Core backed implementation of <see cref="IModuleConsentService"/>.
/// </summary>
/// <remarks>
/// The granted-scope set for a (module, user) pair is cached for
/// <see cref="GrantCacheLifetime"/> because it is evaluated on every user-scoped module call. Every
/// write removes the cache entry before the change is committed, so revocation takes effect on the
/// very next call rather than on the next cache expiry.
/// </remarks>
internal sealed class ModuleConsentService : IModuleConsentService
{
    /// <summary>How long a granted-scope set may be served from cache.</summary>
    internal static readonly TimeSpan GrantCacheLifetime = TimeSpan.FromSeconds(30);

    private readonly CoreDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ModuleConsentService> _logger;

    /// <summary>
    /// Per-module generation counter. Bulk revocation bumps it, which makes every cached entry for
    /// that module unreachable without having to enumerate the cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, long> _moduleGenerations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Generation bumped when every consent row for one user is deleted (account erasure).</summary>
    private long _userGeneration;

    public ModuleConsentService(
        CoreDbContext db,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<ModuleConsentService> logger)
    {
        _db = db;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedScopesAsync(
        string moduleId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || userId == Guid.Empty)
        {
            return [];
        }

        if (_cache.TryGetValue(CacheKey(moduleId, userId), out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var granted = await _db.ModuleUserConsents
            .AsNoTracking()
            .Where(consent => consent.ModuleId == moduleId
                && consent.UserId == userId
                && (consent.ExpiresAtUtc == null || consent.ExpiresAtUtc > now))
            .Select(consent => consent.Scope)
            .ToListAsync(cancellationToken);

        var normalized = NormalizeScopes(granted);
        _cache.Set(CacheKey(moduleId, userId), normalized, GrantCacheLifetime);

        return normalized;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetMissingScopesAsync(
        string moduleId,
        Guid userId,
        IReadOnlyCollection<string> requiredScopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requiredScopes);

        if (requiredScopes.Count == 0)
        {
            return [];
        }

        var granted = await GetGrantedScopesAsync(moduleId, userId, cancellationToken);
        var grantedSet = new HashSet<string>(granted, StringComparer.Ordinal);

        return requiredScopes
            .Where(scope => !grantedSet.Contains(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> HasConsentAsync(
        string moduleId,
        Guid userId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        var granted = await GetGrantedScopesAsync(moduleId, userId, cancellationToken);
        return granted.Contains(scope, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<int> GrantAsync(
        string moduleId,
        Guid userId,
        IReadOnlyCollection<string> scopes,
        Guid? grantedByUserId = null,
        DateTime? expiresAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(scopes);

        var requested = NormalizeScopes(scopes.Where(ModuleScopes.IsConsentScope));
        if (requested.Count == 0 || userId == Guid.Empty)
        {
            return 0;
        }

        InvalidateCache(moduleId, userId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var existing = await _db.ModuleUserConsents
            .Where(consent => consent.ModuleId == moduleId && consent.UserId == userId)
            .ToListAsync(cancellationToken);

        var byScope = existing.ToDictionary(consent => consent.Scope, StringComparer.Ordinal);
        var written = 0;

        foreach (var scope in requested)
        {
            if (byScope.TryGetValue(scope, out var row))
            {
                // Re-granting refreshes the expiry rather than duplicating the row.
                row.GrantedAtUtc = now;
                row.ExpiresAtUtc = expiresAtUtc;
                row.GrantedByUserId = grantedByUserId;
            }
            else
            {
                row = new ModuleUserConsent
                {
                    Id = Guid.CreateVersion7(),
                    ModuleId = moduleId,
                    UserId = userId,
                    Scope = scope,
                    GrantedAtUtc = now,
                    ExpiresAtUtc = expiresAtUtc,
                    GrantedByUserId = grantedByUserId,
                };

                _db.ModuleUserConsents.Add(row);
            }

            written++;
        }

        if (written > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Module {ModuleId} consent granted to user {UserId} for {ScopeCount} scope(s)",
                LogSanitizer.Sanitize(moduleId),
                userId,
                written);
        }

        return written;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAsync(
        string moduleId,
        Guid userId,
        string scope,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        InvalidateCache(moduleId, userId);

        var rows = await _db.ModuleUserConsents
            .Where(consent => consent.ModuleId == moduleId && consent.UserId == userId && consent.Scope == scope)
            .ToListAsync(cancellationToken);

        _db.ModuleUserConsents.RemoveRange(rows);
        var removed = rows.Count;
        if (removed > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (removed > 0)
        {
            _logger.LogInformation(
                "Module {ModuleId} consent revoked for user {UserId} scope {Scope} by {RevokedBy}",
                LogSanitizer.Sanitize(moduleId),
                userId,
                LogSanitizer.Sanitize(scope),
                revokedByUserId is null ? "user" : "administrator");
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllAsync(
        string moduleId,
        Guid userId,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        InvalidateCache(moduleId, userId);

        var rows = await _db.ModuleUserConsents
            .Where(consent => consent.ModuleId == moduleId && consent.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.ModuleUserConsents.RemoveRange(rows);
        var removed = rows.Count;
        if (removed > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (removed > 0)
        {
            _logger.LogInformation(
                "All module {ModuleId} consent revoked for user {UserId} by {RevokedBy}",
                LogSanitizer.Sanitize(moduleId),
                userId,
                revokedByUserId is null ? "user" : "administrator");
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllForModuleAsync(
        string moduleId,
        Guid? revokedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var rows = await _db.ModuleUserConsents
            .Where(consent => consent.ModuleId == moduleId)
            .ToListAsync(cancellationToken);

        _db.ModuleUserConsents.RemoveRange(rows);
        var removed = rows.Count;
        if (removed > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        _moduleGenerations.AddOrUpdate(moduleId, 1, static (_, current) => current + 1);

        if (removed > 0)
        {
            _logger.LogInformation(
                "Removed {Count} consent row(s) for uninstalled module {ModuleId}",
                removed,
                LogSanitizer.Sanitize(moduleId));
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return 0;
        }

        var rows = await _db.ModuleUserConsents
            .Where(consent => consent.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.ModuleUserConsents.RemoveRange(rows);
        var removed = rows.Count;
        if (removed > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        Interlocked.Increment(ref _userGeneration);

        return removed;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModuleConsentSummary>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var rows = await (
                from consent in _db.ModuleUserConsents.AsNoTracking()
                join module in _db.InstalledModules.AsNoTracking() on consent.ModuleId equals module.ModuleId
                where consent.UserId == userId && (consent.ExpiresAtUtc == null || consent.ExpiresAtUtc > now)
                select new
                {
                    consent.ModuleId,
                    ModuleName = module.ModuleId,
                    module.Status,
                    consent.Scope,
                    consent.GrantedAtUtc,
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new { row.ModuleId, row.ModuleName, row.Status })
            .Select(group => new ModuleConsentSummary(
                group.Key.ModuleId,
                group.Key.ModuleName,
                NormalizeScopes(group.Select(row => row.Scope)),
                group.Max(row => row.GrantedAtUtc),
                IsInstalled(group.Key.Status)))
            .OrderBy(summary => summary.ModuleName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<ModuleConsentUserSummary>> ListForModuleAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var rows = await _db.ModuleUserConsents
            .AsNoTracking()
            .Where(consent => consent.ModuleId == moduleId
                && (consent.ExpiresAtUtc == null || consent.ExpiresAtUtc > now))
            .Select(consent => new
            {
                consent.UserId,
                consent.Scope,
                consent.GrantedAtUtc,
                consent.GrantedByUserId,
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .Select(group => new ModuleConsentUserSummary(
                group.Key,
                NormalizeScopes(group.Select(row => row.Scope)),
                group.Max(row => row.GrantedAtUtc),
                group.Any(row => row.GrantedByUserId is not null)))
            .OrderBy(summary => summary.UserId)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<ModuleConsentOverview> GetOverviewAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var rows = await _db.ModuleUserConsents
            .AsNoTracking()
            .Where(consent => consent.ModuleId == moduleId
                && (consent.ExpiresAtUtc == null || consent.ExpiresAtUtc > now))
            .Select(consent => new { consent.UserId, consent.Scope })
            .ToListAsync(cancellationToken);

        var scopeCounts = rows
            .GroupBy(row => row.Scope)
            .Select(group => new ModuleScopeConsentCount(group.Key, group.Select(row => row.UserId).Distinct().Count()))
            .OrderBy(count => count.Scope, StringComparer.Ordinal)
            .ToArray();

        return new ModuleConsentOverview(
            rows.Select(row => row.UserId).Distinct().Count(),
            scopeCounts);
    }

    /// <inheritdoc />
    public void InvalidateCache(string moduleId, Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            _cache.Remove(CacheKey(moduleId, userId));
        }
    }

    private string CacheKey(string moduleId, Guid userId) =>
        $"dnc:moduleconsent:{moduleId}:{Volatile.Read(ref _userGeneration)}"
        + $":{(_moduleGenerations.TryGetValue(moduleId, out var generation) ? generation : 0)}:{userId:N}";

    private static bool IsInstalled(string status) =>
        !string.Equals(status, "Uninstalling", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> NormalizeScopes(IEnumerable<string> scopes) =>
        scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ScopeOrder)
            .ToArray();

    /// <summary>Orders known scopes by their catalog position so UI listings are stable.</summary>
    private static int ScopeOrder(string scope)
    {
        var index = 0;
        foreach (var definition in ModuleScopes.ConsentScopes)
        {
            if (string.Equals(definition.Name, scope, StringComparison.Ordinal))
            {
                return index;
            }

            index++;
        }

        return int.MaxValue;
    }
}
