using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNetCloud.Modules.AI.Host.Controllers;

/// <summary>
/// Maps the exceptions the AI API can raise to correct HTTP status codes.
/// </summary>
/// <remarks>
/// A sandboxed module has no cookie scheme, so a missing or unverifiable end-user assertion surfaces
/// as an <see cref="UnauthorizedAccessException"/> from the controller. Without this filter ASP.NET
/// Core would report it as 500, which reads like a module defect instead of an authentication
/// failure (this was a real defect found in the Bookmarks conversion).
/// </remarks>
public sealed class AiExceptionFilter : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.Exception)
        {
            case UnauthorizedAccessException:
                context.Result = new UnauthorizedObjectResult(new { success = false, error = new { code = "UNAUTHORIZED", message = "Authentication is required." } });
                break;

            case ArgumentException:
                context.Result = new BadRequestObjectResult(new { success = false, error = new { code = "INVALID_REQUEST", message = context.Exception.Message } });
                break;

            default:
                return;
        }

        context.ExceptionHandled = true;
    }
}
