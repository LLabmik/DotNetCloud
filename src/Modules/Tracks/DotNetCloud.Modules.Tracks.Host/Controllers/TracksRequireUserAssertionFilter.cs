using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// Requires a verified end-user assertion for every action of this module's REST API.
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module has no cookie scheme, so the controller-level
/// <c>[Authorize(AuthenticationSchemes = "Identity.Application")]</c> the module used to carry no
/// longer applies to anything. Most actions take the acting user from
/// <see cref="TracksControllerBase.GetAuthenticatedCaller"/> and fail closed on their own, but several
/// actions (share links, guest access, product templates, sprint discussions, webhook deliveries and
/// the recurring-rule sweep) never resolve a caller at all. Without this gate they would be reachable
/// anonymously through the core proxy once the cookie handler is gone.
/// </para>
/// <para>
/// This is an authorization filter rather than an action filter so it runs before model binding and
/// before the exception filter, and answers 401 with the module's usual error envelope.
/// </para>
/// </remarks>
public sealed class TracksRequireUserAssertionFilter : IAuthorizationFilter
{
    /// <inheritdoc />
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var accessor = context.HttpContext.RequestServices.GetService<IModuleUserAssertionAccessor>();
        if (accessor?.GetCurrentUserId() is { } userId && userId != Guid.Empty)
        {
            return;
        }

        context.Result = new UnauthorizedObjectResult(new
        {
            success = false,
            error = new { code = "UNAUTHORIZED", message = "Authentication is required." },
        });
    }
}
