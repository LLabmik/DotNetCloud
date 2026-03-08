using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Base controller for all Files module REST API controllers.
/// Provides helper methods for caller context creation, envelope responses, and exception handling.
/// </summary>
[ApiController]
[Authorize]
public abstract class FilesControllerBase : ControllerBase
{
    /// <summary>
    /// Creates a <see cref="CallerContext"/> from the authenticated bearer token claims.
    /// </summary>
    protected CallerContext GetAuthenticatedCaller()
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new ForbiddenException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var userId))
            throw new ForbiddenException("Authenticated user identifier is invalid.");

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(userId, roles, CallerType.User);
    }

    /// <summary>
    /// Creates a <see cref="CallerContext"/> for the given user ID.
    /// </summary>
    protected CallerContext ToCaller(Guid userId)
    {
        if (User?.Identity?.IsAuthenticated != true)
            throw new ForbiddenException("Authentication is required.");

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claimValue, out var authenticatedUserId))
            throw new ForbiddenException("Authenticated user identifier is invalid.");

        if (authenticatedUserId != userId)
            throw new ForbiddenException("Caller user ID does not match the authenticated identity.");

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CallerContext(authenticatedUserId, roles, CallerType.User);
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
    }
}
