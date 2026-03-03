using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using DotNetCloud.Core.Errors;

namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Middleware for global exception handling.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly bool _includeStackTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionHandlerMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="includeStackTrace">Whether to include stack traces in error responses.</param>
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        bool includeStackTrace = false)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _includeStackTrace = includeStackTrace;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred while processing request {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        var (statusCode, errorCode, message) = MapException(exception);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Code = errorCode,
            Message = message,
            RequestId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (_includeStackTrace)
        {
            errorResponse.Details = new
            {
                exceptionType = exception.GetType().Name,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException?.Message
            };
        }

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }

    private (int statusCode, string errorCode, string message) MapException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedException => (
                (int)HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                exception.Message),

            CapabilityNotGrantedException => (
                (int)HttpStatusCode.Forbidden,
                "CAPABILITY_NOT_GRANTED",
                exception.Message),

            ValidationException => (
                (int)HttpStatusCode.BadRequest,
                "VALIDATION_ERROR",
                exception.Message),

            ModuleNotFoundException => (
                (int)HttpStatusCode.NotFound,
                "MODULE_NOT_FOUND",
                exception.Message),

            ArgumentNullException or ArgumentException => (
                (int)HttpStatusCode.BadRequest,
                "INVALID_ARGUMENT",
                exception.Message),

            System.InvalidOperationException => (
                (int)HttpStatusCode.Conflict,
                "INVALID_OPERATION",
                exception.Message),

            NotImplementedException => (
                (int)HttpStatusCode.NotImplemented,
                "NOT_IMPLEMENTED",
                exception.Message),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred. Please try again later.")
        };
    }

    private class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public object? Details { get; set; }
    }
}
