using System.Security.Claims;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Security;

/// <summary>
/// Enriches the <see cref="ClaimsPrincipal"/> on each authenticated request with
/// DotNetCloud-specific claims sourced from the database.
/// </summary>
/// <remarks>
/// Added claims include application roles (<c>role</c>), organization ID (<c>dnc:org</c>),
/// locale (<c>dnc:locale</c>), and timezone (<c>dnc:tz</c>). Results are cached per-user
/// in a short-lived <see cref="IMemoryCache"/> entry to reduce database round-trips.
/// </remarks>
public sealed class DotNetCloudClaimsTransformation : IClaimsTransformation
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DotNetCloudClaimsTransformation> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DotNetCloudClaimsTransformation"/>.
    /// </summary>
    public DotNetCloudClaimsTransformation(
        UserManager<ApplicationUser> userManager,
        IMemoryCache cache,
        ILogger<DotNetCloudClaimsTransformation> logger)
    {
        _userManager = userManager;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (userId is null)
        {
            return principal;
        }

        var cacheKey = $"claims:{userId}";
        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<Claim>? additionalClaims))
        {
            additionalClaims = await BuildAdditionalClaimsAsync(userId);
            _cache.Set(cacheKey, additionalClaims, CacheTtl);
        }

        if (additionalClaims is null || additionalClaims.Count == 0)
        {
            return principal;
        }

        // Clone the principal and add the enriched identity
        var clonedIdentity = new ClaimsIdentity(principal.Identity);
        clonedIdentity.AddClaims(additionalClaims.Where(c =>
            !clonedIdentity.HasClaim(c.Type, c.Value)));

        return new ClaimsPrincipal(clonedIdentity);
    }

    private async Task<IReadOnlyList<Claim>> BuildAdditionalClaimsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("ClaimsTransformation: user {UserId} not found", userId);
            return [];
        }

        var claims = new List<Claim>();

        // Add application roles
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Add display name so Blazor pages can resolve it from the principal
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("name", user.DisplayName));
        }

        // Add DotNetCloud-specific claims
        claims.Add(new Claim("dnc:locale", user.Locale));
        claims.Add(new Claim("dnc:tz", user.Timezone));

        return claims.AsReadOnly();
    }
}
