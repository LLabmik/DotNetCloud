using System.Security.Claims;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.Auth.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> for DotNetCloud authorization checks.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns true if the principal has the System Administrator role.
    /// Checks both Identity role membership and the <c>dnc:perm</c> claim.
    /// </summary>
    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(SystemRoleNames.Administrator)
            || principal.HasClaim(PermissionAuthorizationHandler.PermissionClaimType, "admin");
    }
}
