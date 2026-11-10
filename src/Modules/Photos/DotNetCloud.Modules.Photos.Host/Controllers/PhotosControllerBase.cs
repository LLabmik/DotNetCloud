using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Grpc;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Photos.Host.Controllers;

/// <summary>
/// Base controller for Photos module endpoints. Provides authentication helpers and response envelope methods.
/// </summary>
/// <remarks>
/// There is no cookie scheme in this host: the core reverse-proxy strips the browser's session and
/// mints a short-lived, module-audience <c>dnc-user-assertion</c> instead. The acting user therefore
/// comes from <see cref="IModuleUserAssertionAccessor"/> and never from a request field — the core
/// enforces the matching half of that invariant on its own side of every brokered call.
/// </remarks>
[ApiController]
public abstract class PhotosControllerBase : ControllerBase
{
    /// <summary>
    /// Extracts the authenticated caller context from the verified core assertion.
    /// </summary>
    /// <returns>The acting user.</returns>
    /// <exception cref="UnauthorizedAccessException">No verified assertion is present.</exception>
    protected CallerContext GetAuthenticatedCaller()
    {
        var accessor = HttpContext.RequestServices?.GetService<IModuleUserAssertionAccessor>();
        var userId = accessor?.GetCurrentUserId();

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        // Roles are not carried on the module assertion: the core re-resolves them for every call it
        // brokers, so a module has no business trusting a role claim it cannot verify.
        return new CallerContext(userId.Value, [], CallerType.User);
    }

    /// <summary>
    /// Wraps data in a standard success envelope.
    /// </summary>
    protected static object Envelope(object data)
    {
        return new { success = true, data };
    }

    /// <summary>
    /// Wraps data and pagination in a standard success envelope.
    /// </summary>
    protected static object Envelope(object data, object pagination)
    {
        return new { success = true, data, pagination };
    }

    /// <summary>
    /// Creates a standard error envelope.
    /// </summary>
    protected static object ErrorEnvelope(string code, string message)
    {
        return new { success = false, error = new { code, message } };
    }

    /// <summary>
    /// Executes an async action with standard exception-to-HTTP-status mapping.
    /// </summary>
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
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
