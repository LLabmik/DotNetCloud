using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DotNetCloud.Modules.Contacts.Host.Controllers;

/// <summary>
/// Base controller for all Contacts module REST API controllers.
/// Provides helper methods for caller context creation and envelope responses.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
public abstract class ContactsControllerBase : ControllerBase
{
    /// <summary>
    /// Creates a <see cref="CallerContext"/> from the authenticated bearer token claims.
    /// </summary>
    protected CallerContext GetAuthenticatedCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var userId))
            throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }

    /// <summary>
    /// Wraps data in a standard success envelope.
    /// </summary>
    protected static object Envelope(object data)
    {
        return new { success = true, data };
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
