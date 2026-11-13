using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNetCloud.Modules.Bookmarks.Host.Controllers;

/// <summary>
/// Maps the module's domain exceptions to HTTP responses for every action.
/// </summary>
/// <remarks>
/// <para>
/// Actions that catch and translate exceptions themselves never reach this filter. It exists for the
/// ones that do not: the module is sandboxed and authenticates nobody itself, so an unauthenticated
/// request surfaces as <see cref="UnauthorizedAccessException"/> from
/// <see cref="BookmarksControllerBase.GetAuthenticatedCaller"/>. Without this mapping that
/// exception escaped as a 500 — the UI then reported "the request failed" instead of asking the user
/// to sign in, and the failure looked like a module bug in the logs.
/// </para>
/// <para>
/// Status codes follow the same table as <see cref="BookmarksControllerBase.ExecuteAsync"/>; the
/// error body is the module's standard envelope so a client parses one shape either way.
/// </para>
/// </remarks>
public sealed class BookmarksExceptionFilter : IExceptionFilter
{
    private readonly ILogger<BookmarksExceptionFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public BookmarksExceptionFilter(ILogger<BookmarksExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.Exception)
        {
            case UnauthorizedAccessException ex:
                // Not logged: an unauthenticated request is expected traffic (the UI probes before the
                // proxy mints an assertion), not an error worth an entry per request.
                context.Result = Envelope(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", ex.Message);
                context.ExceptionHandled = true;
                break;

            case NotFoundException ex:
                context.Result = Envelope(StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

            case ForbiddenException ex:
                context.Result = Envelope(StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

            case Core.Errors.InvalidOperationException ex:
                context.Result = Envelope(StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

            case ValidationException ex:
                context.Result = Envelope(StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

            default:
                // Left unhandled so the host's error pipeline reports it; logged here once with the
                // action name so the module's own log has the context.
                _logger.LogError(
                    context.Exception,
                    "Unhandled exception in {Action}",
                    context.ActionDescriptor?.DisplayName);
                break;
        }
    }

    private static ObjectResult Envelope(int statusCode, string code, string message) =>
        new(new { success = false, error = new { code, message } })
        {
            StatusCode = statusCode,
        };
}
