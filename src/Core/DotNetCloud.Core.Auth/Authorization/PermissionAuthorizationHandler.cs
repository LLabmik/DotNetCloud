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
        if (context.User.HasClaim(
                c => c.Type == PermissionClaimType && c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
