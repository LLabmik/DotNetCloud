using DotNetCloud.Core.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace DotNetCloud.Core.Auth.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> by checking the <c>dnc:perm</c> claims
/// on the current principal.
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// The claim type used to carry DotNetCloud permission values.
    /// </summary>
    public const string PermissionClaimType = "dnc:perm";

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Seeded installations grant the Identity role "Administrator".
        // Treat that role as satisfying the admin permission requirement.
        if (string.Equals(requirement.Permission, "admin", StringComparison.OrdinalIgnoreCase)
            && context.User.IsInRole(SystemRoleNames.Administrator))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(
                c => c.Type == PermissionClaimType && c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
