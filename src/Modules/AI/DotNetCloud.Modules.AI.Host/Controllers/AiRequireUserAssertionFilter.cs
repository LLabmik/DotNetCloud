using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.AI.Host.Controllers;

/// <summary>
/// Requires a verified end-user assertion for every action of this module's REST API.
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module has no cookie scheme, so the controller-level <c>[Authorize]</c> the module used
/// to carry no longer applies to anything. Actions that take the acting user from
/// <see cref="AiChatController.GetCallerContext"/> fail closed on their own, but actions that do not
/// need a user (the Ollama health probe and the settings read-out) would otherwise be reachable
/// anonymously through the core proxy. They are still module data — the provider and model an
/// administrator configured — so the whole surface stays behind the same gate.
/// </para>
/// <para>
/// This is an authorization filter rather than an action filter so it runs before model binding and
/// before the exception filter, and answers 401 with the module's usual error envelope.
/// </para>
/// </remarks>
public sealed class AiRequireUserAssertionFilter : IAuthorizationFilter
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
