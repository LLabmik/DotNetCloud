using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// Maps the module's domain exceptions to HTTP responses for every action.
/// </summary>
/// <remarks>
/// <para>
/// The module is a sandboxed process and authenticates nobody itself: the core proxies every request
/// with a short-lived, module-audience assertion, so an unauthenticated or expired request surfaces as
/// <see cref="UnauthorizedAccessException"/> from
/// <see cref="TracksControllerBase.GetAuthenticatedCaller"/>. Without this mapping that exception
/// escaped as a 500 — the UI then reported "the request failed" instead of asking the user to sign in,
/// and the failure looked like a module bug in the logs.
/// </para>
/// <para>
/// Controllers that still catch exceptions themselves keep their own mapping; this filter is the
/// safety net for everything that reaches the pipeline, and it uses the module's standard error
/// envelope so a client parses one shape either way.
/// </para>
/// </remarks>
public sealed class TracksExceptionFilter : IExceptionFilter
{
    private readonly ILogger<TracksExceptionFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracksExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public TracksExceptionFilter(ILogger<TracksExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mapped = Map(context.Exception);
        if (mapped is not null)
        {
            // Not logged: a 401 is expected traffic (the UI probes before the proxy mints an
            // assertion) and the client-caused statuses below are already reported to the caller.
            context.Result = mapped;
            context.ExceptionHandled = true;
            return;
        }

        _logger.LogError(
            context.Exception,
            "Unhandled exception in {Controller}.{Action}",
            context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller)
                ? controller
                : "Tracks",
            context.ActionDescriptor.DisplayName);
    }

    private static ObjectResult? Map(Exception exception) => exception switch
    {
        UnauthorizedAccessException unauthorized =>
            Envelope(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", unauthorized.Message),
        UnauthorizedException unauthorized =>
            Envelope(StatusCodes.Status401Unauthorized, unauthorized.ErrorCode, unauthorized.Message),
        NotFoundException notFound =>
            Envelope(StatusCodes.Status404NotFound, notFound.ErrorCode, notFound.Message),
        ForbiddenException forbidden =>
            Envelope(StatusCodes.Status403Forbidden, forbidden.ErrorCode, forbidden.Message),
        CapabilityNotGrantedException capability =>
            Envelope(StatusCodes.Status403Forbidden, capability.ErrorCode, capability.Message),
        ValidationException validation =>
            Envelope(StatusCodes.Status409Conflict, validation.ErrorCode, validation.Message),
        NameConflictException conflict =>
            Envelope(StatusCodes.Status409Conflict, conflict.ErrorCode, conflict.Message),
        ConcurrencyException concurrency =>
            Envelope(StatusCodes.Status409Conflict, concurrency.ErrorCode, concurrency.Message),
        BusinessRuleException businessRule =>
            Envelope(StatusCodes.Status409Conflict, businessRule.ErrorCode, businessRule.Message),
        FileReadOnlyException readOnly =>
            Envelope(StatusCodes.Status409Conflict, readOnly.ErrorCode, readOnly.Message),
        DotNetCloud.Core.Errors.InvalidOperationException invalid =>
            Envelope(StatusCodes.Status400BadRequest, invalid.ErrorCode, invalid.Message),
        _ => null,
    };

    private static ObjectResult Envelope(int statusCode, string code, string message) =>
        new(new { success = false, error = new { code, message } }) { StatusCode = statusCode };
}
