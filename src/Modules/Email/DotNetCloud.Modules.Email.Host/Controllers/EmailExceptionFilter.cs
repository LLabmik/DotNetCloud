using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// Maps the module's domain exceptions to HTTP responses for every action.
/// </summary>
/// <remarks>
/// <para>
/// Actions that catch and translate exceptions themselves never reach this filter. It exists for the
/// ones that do not: the module is sandboxed and authenticates nobody itself, so an unauthenticated
/// request surfaces as <see cref="UnauthorizedAccessException"/> from
/// <see cref="EmailControllerBase.GetAuthenticatedCaller"/>. Without this mapping that exception
/// escaped as a 500 — the UI then reported "the request failed" instead of asking the user to sign
/// in, and the failure looked like a module bug in the logs.
/// </para>
/// <para>
/// Status codes follow the same table as <see cref="EmailControllerBase.ExecuteAsync"/>; the error
/// body is the module's standard envelope so a client parses one shape either way.
/// </para>
/// </remarks>
public sealed class EmailExceptionFilter : IExceptionFilter
{
    private readonly ILogger<EmailExceptionFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public EmailExceptionFilter(ILogger<EmailExceptionFilter> logger)
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

            case Grpc.Core.RpcException ex:
                // A call the module made to the core failed. The UI has three distinct conversations
                // to have here — ask the user for consent, say files are unavailable, say the file is
                // gone — so the core's status is translated instead of collapsing to 500.
                var (status, code) = MapCoreFailure(ex);
                context.Result = Envelope(status, code, ex.Status.Detail);
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
            ContentTypes = { "application/json" },
        };

    /// <summary>
    /// Maps a failed core call onto the module's error surface.
    /// </summary>
    /// <param name="exception">The core call failure.</param>
    /// <returns>The HTTP status and the module-level error code.</returns>
    private static (int Status, string Code) MapCoreFailure(Grpc.Core.RpcException exception)
    {
        var coreCode = exception.Trailers?.GetValue("error-code");

        return exception.StatusCode switch
        {
            Grpc.Core.StatusCode.PermissionDenied when string.Equals(
                coreCode, "MODULE_CONSENT_REQUIRED", StringComparison.Ordinal)
                => (StatusCodes.Status403Forbidden, "FILES_CONSENT_REQUIRED"),

            Grpc.Core.StatusCode.PermissionDenied => (StatusCodes.Status403Forbidden, "FILES_ACCESS_DENIED"),
            Grpc.Core.StatusCode.Unauthenticated => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),
            Grpc.Core.StatusCode.NotFound => (StatusCodes.Status404NotFound, "FILE_NOT_FOUND"),
            Grpc.Core.StatusCode.ResourceExhausted => (StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE"),
            Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.Unimplemented
                => (StatusCodes.Status503ServiceUnavailable, "FILES_UNAVAILABLE"),
            Grpc.Core.StatusCode.InvalidArgument => (StatusCodes.Status400BadRequest, "FILE_REQUEST_INVALID"),
            Grpc.Core.StatusCode.FailedPrecondition => (StatusCodes.Status400BadRequest, "FILES_REJECTED"),
            _ => (StatusCodes.Status502BadGateway, "FILES_ERROR"),
        };
    }
}
