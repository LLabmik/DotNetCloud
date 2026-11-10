using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNetCloud.Modules.Photos.Host.Controllers;

/// <summary>
/// Maps the module's domain exceptions to HTTP responses for every action.
/// </summary>
/// <remarks>
/// <para>
/// The module is sandboxed and authenticates nobody itself: the core proxies every request with a
/// short-lived, module-audience assertion, so an unauthenticated or expired request surfaces as
/// <see cref="UnauthorizedAccessException"/> from
/// <see cref="PhotosControllerBase.GetAuthenticatedCaller"/>. Without this mapping that exception
/// escaped as a 500 — the UI then reported "the request failed" instead of asking the user to sign
/// in, and the failure looked like a module bug in the logs.
/// </para>
/// <para>
/// Status codes follow the same table as <see cref="PhotosControllerBase.ExecuteAsync"/>; the error
/// body is the module's standard envelope so a client parses one shape either way.
/// </para>
/// </remarks>
public sealed class PhotosExceptionFilter : IExceptionFilter
{
    private readonly ILogger<PhotosExceptionFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public PhotosExceptionFilter(ILogger<PhotosExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        switch (context.Exception)
        {
            case Data.Services.FilesConsentRequiredException ex:
                // Not a failure: the user has not granted `files:read` yet, and the UI answers this code
                // by asking the shell to show its consent prompt.
                context.Result = Envelope(StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

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

            case ValidationException ex:
                context.Result = Envelope(StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;

            case Core.Errors.InvalidOperationException ex:
                context.Result = Envelope(StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message);
                context.ExceptionHandled = true;
                break;
        }

        if (context.ExceptionHandled)
        {
            return;
        }

        _logger.LogError(
            context.Exception,
            "Unhandled exception in {Controller}.{Action}",
            context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
                ? controller
                : "Photos",
            context.ActionDescriptor.DisplayName);
    }

    private static ObjectResult Envelope(int statusCode, string code, string message) =>
        new(new { success = false, error = new { code, message } }) { StatusCode = statusCode };
}
