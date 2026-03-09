using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Middleware that propagates request correlation IDs via the <c>X-Request-ID</c> header.
/// If the incoming request carries an <c>X-Request-ID</c> header, that value is used;
/// otherwise a new compact GUID is generated. The correlation ID is written back on the
/// response and set as <see cref="HttpContext.TraceIdentifier"/> so all downstream
/// logging (including <see cref="RequestResponseLoggingMiddleware"/>) uses the same value.
/// </summary>
public class RequestCorrelationMiddleware
{
    private const string HeaderName = "X-Request-ID";
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestCorrelationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public RequestCorrelationMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Processes the request, ensuring a correlation ID is present on the response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("RequestId", requestId))
        {
            await _next(context);
        }
    }
}
