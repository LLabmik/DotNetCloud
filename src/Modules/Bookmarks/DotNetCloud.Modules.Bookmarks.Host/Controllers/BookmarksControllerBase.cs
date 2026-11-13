using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Host.Controllers;

/// <summary>
/// Base controller for Bookmarks module endpoints. Provides authentication helpers and response envelope methods.
/// </summary>
/// <remarks>
/// The module is sandboxed: the core proxy strips the user's session cookie and sends a short-lived
/// assertion bound to this module. There is no cookie scheme here, so <c>[Authorize]</c> cannot be the
/// gate — <see cref="GetAuthenticatedCaller"/> is, and a request without a verifiable assertion is
/// answered with 401 rather than being treated as anonymous.
/// </remarks>
[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class BookmarksControllerBase : ControllerBase
{
    /// <summary>
    /// Extracts the authenticated caller context from the current request.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The request carries no verified end-user assertion.</exception>
    protected CallerContext GetAuthenticatedCaller()
    {
        var accessor = HttpContext.RequestServices.GetService<IModuleUserAssertionAccessor>();
        if (accessor?.GetCurrentUserId() is not { } userId || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        // Roles are resolved from the core-signed assertion's user identity; the module never trusts
        // a role header. Claims from the current principal are used only when the core supplied them.
        var roles = User?.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        return new CallerContext(userId, roles, CallerType.User);
    }

    /// <summary>Wraps data in a success envelope.</summary>
    protected static object Envelope(object data) => new { success = true, data };

    /// <summary>Creates an error envelope.</summary>
    protected static object ErrorEnvelope(string code, string message)
        => new { success = false, error = new { code, message } };

    /// <summary>
    /// Executes an async action with standard exception-to-HTTP-status mapping.
    /// </summary>
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ErrorEnvelope("UNAUTHORIZED", ex.Message));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
        catch (ValidationException ex)
        {
            return Conflict(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
        catch (Core.Errors.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger(GetType());
            logger?.LogError(ex, "Unhandled exception in {Controller}.{Action}",
                GetType().Name, HttpContext.GetEndpoint()?.DisplayName);
            return StatusCode(500, ErrorEnvelope("INTERNAL_ERROR", "An unexpected error occurred."));
        }
    }
}
