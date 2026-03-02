using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using DotNetCloud.Core.ServiceDefaults.Logging;

namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly HashSet<string> _sensitiveHeaders;
    private readonly HashSet<string> _excludedPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestResponseLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Auth-Token"
        };

        _excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/health",
            "/metrics"
        };
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for excluded paths
        if (_excludedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await _next(context);
            return;
        }

        var requestId = context.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();

        using (LogEnricher.WithRequestId(requestId))
        {
            LogRequest(context);

            var originalBodyStream = context.Response.Body;

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                LogResponse(context, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private void LogRequest(HttpContext context)
    {
        var request = context.Request;
        
        var headers = MaskSensitiveHeaders(request.Headers);

        _logger.LogInformation(
            "HTTP {Method} {Path} started from {RemoteIp}",
            request.Method,
            request.Path,
            context.Connection.RemoteIpAddress);

        _logger.LogDebug(
            "Request details: {Method} {Scheme}://{Host}{Path}{QueryString} Headers: {@Headers}",
            request.Method,
            request.Scheme,
            request.Host,
            request.Path,
            request.QueryString,
            headers);
    }

    private void LogResponse(HttpContext context, long elapsedMs)
    {
        var response = context.Response;

        var logLevel = response.StatusCode >= 500
            ? LogLevel.Error
            : response.StatusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;

        _logger.Log(
            logLevel,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            context.Request.Path,
            response.StatusCode,
            elapsedMs);
    }

    private Dictionary<string, string> MaskSensitiveHeaders(IHeaderDictionary headers)
    {
        var maskedHeaders = new Dictionary<string, string>();

        foreach (var (key, value) in headers)
        {
            if (_sensitiveHeaders.Contains(key))
            {
                maskedHeaders[key] = "***REDACTED***";
            }
            else
            {
                maskedHeaders[key] = value.ToString();
            }
        }

        return maskedHeaders;
    }
}
