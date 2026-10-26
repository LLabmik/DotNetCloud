using System.Collections.Concurrent;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// Resolves the authoritative role set for an end user named on a module capability call.
/// </summary>
internal interface ICallerIdentityResolver
{
    /// <summary>
    /// Resolves the roles held by <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">The end-user identifier the module claims to act for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The user's current roles, or <see langword="null"/> when the account does not exist or is
    /// inactive — in which case the call must be rejected.
    /// </returns>
    Task<IReadOnlyList<string>?> ResolveRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="ICallerIdentityResolver"/> backed by ASP.NET Identity, with a short
/// time-to-live cache so a high-volume module does not issue a database query per capability call.
/// </summary>
/// <remarks>
/// The cache window is deliberately short (one minute): role changes and account disables are
/// containment actions, and the credentials they are meant to revoke should not outlive them for
/// long. This is intentionally stricter than the five-minute window used for browser claim
/// enrichment.
/// </remarks>
internal sealed class CallerIdentityResolver : ICallerIdentityResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CallerIdentityResolver> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();

    public CallerIdentityResolver(
        IServiceScopeFactory scopeFactory,
        ILogger<CallerIdentityResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> ResolveRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(userId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Roles;
        }

        IReadOnlyList<string>? roles;

        using (var scope = _scopeFactory.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());

            if (user is null || !user.IsActive)
            {
                _logger.LogWarning(
                    "Rejected capability call for end user {UserId}: account is missing or inactive",
                    userId);
                roles = null;
            }
            else
            {
                var assigned = await userManager.GetRolesAsync(user);
                roles = assigned.ToArray();
            }
        }

        if (roles is not null)
        {
            _cache[userId] = new CacheEntry(roles, DateTimeOffset.UtcNow.Add(CacheTtl));
        }
        else
        {
            _cache.TryRemove(userId, out _);
        }

        return roles;
    }

    private sealed record CacheEntry(IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);
}
