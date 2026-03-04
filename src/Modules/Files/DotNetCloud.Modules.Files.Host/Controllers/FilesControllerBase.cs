using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Base controller for all Files module REST API controllers.
/// Provides helper methods for caller context creation, envelope responses, and exception handling.
/// </summary>
[ApiController]
public abstract class FilesControllerBase : ControllerBase
{
    /// <summary>
    /// Creates a <see cref="CallerContext"/> for the given user ID.
    /// </summary>
    protected static CallerContext ToCaller(Guid userId)
    {
        return new CallerContext(userId, ["user"], CallerType.User);
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
