using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="ICurrentUserContext"/> by extracting a <see cref="CallerContext"/>
/// from the current HTTP request's JWT claims.
/// </summary>
public sealed class CurrentUserContextService : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="CurrentUserContextService"/>.
    /// </summary>
    public CurrentUserContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Builds a <see cref="CallerContext"/> from the current request's authenticated principal.
    /// </summary>
    /// <returns>
    /// A <see cref="CallerContext"/> for the authenticated user, or <see langword="null"/>
    /// if no authenticated principal is present.
    /// </returns>
    public CallerContext? GetCurrentCaller()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subClaim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (!Guid.TryParse(subClaim, out var userId))
        {
            return null;
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }
}
