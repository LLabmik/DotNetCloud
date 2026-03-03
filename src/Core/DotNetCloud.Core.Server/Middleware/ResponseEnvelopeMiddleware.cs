using System.Text.Json;
using DotNetCloud.Core.Errors;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Configuration options for response envelope middleware.
/// </summary>
public sealed class ResponseEnvelopeOptions
{
    /// <summary>
    /// Gets or sets whether the envelope is applied to all responses.
    /// If false, the envelope is only applied when explicitly requested. Defaults to true.
    /// </summary>
    public bool EnableForAll { get; set; } = true;

    /// <summary>
    /// Gets or sets path prefixes that should be enveloped.
    /// If empty, all API paths are enveloped. Defaults to ["/api/"].
    /// </summary>
    public string[] IncludePaths { get; set; } = ["/api/"];

    /// <summary>
    /// Gets or sets path prefixes that should be excluded from enveloping.
    /// Defaults to common non-API paths.
    /// </summary>
    public string[] ExcludePaths { get; set; } =
    [
        "/health",
        "/openapi",
        "/swagger",
        "/connect/",
        "/hubs/"
    ];
}

/// <summary>
/// Middleware that wraps API responses in a standard envelope format.
/// Successful responses are wrapped in <see cref="ApiSuccessResponse{T}"/>.
/// Error responses are wrapped in <see cref="ApiErrorResponse"/>.
/// </summary>
public sealed class ResponseEnvelopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ResponseEnvelopeOptions _options;
    private readonly ILogger<ResponseEnvelopeMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseEnvelopeMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The response envelope options.</param>
    /// <param name="logger">The logger.</param>
    public ResponseEnvelopeMiddleware(
        RequestDelegate next,
        ResponseEnvelopeOptions options,
        ILogger<ResponseEnvelopeMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldEnvelope(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Buffer the response body so we can inspect and potentially wrap it
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        memoryStream.Position = 0;
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
        context.Response.Body = originalBodyStream;

        // Don't envelope empty responses (204 No Content, 304 Not Modified, etc.)
        if (string.IsNullOrEmpty(responseBody) ||
            context.Response.StatusCode == StatusCodes.Status204NoContent ||
            context.Response.StatusCode == StatusCodes.Status304NotModified)
        {
            if (!string.IsNullOrEmpty(responseBody))
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);
            }
            return;
        }

        // Don't re-envelope if the response is already enveloped
        if (IsAlreadyEnveloped(responseBody))
        {
            await WriteToResponseAsync(context, responseBody);
            return;
        }

        // Only envelope JSON responses
        if (!IsJsonContentType(context.Response.ContentType))
        {
            await WriteToResponseAsync(context, responseBody);
            return;
        }

        var statusCode = context.Response.StatusCode;

        string envelopedResponse;

        if (statusCode >= 200 && statusCode < 300)
        {
            envelopedResponse = WrapSuccessResponse(responseBody);
        }
        else
        {
            envelopedResponse = WrapErrorResponse(responseBody, statusCode, context.TraceIdentifier);
        }

        context.Response.ContentType = "application/json";
        context.Response.ContentLength = null; // Recalculate content length
        await context.Response.WriteAsync(envelopedResponse);
    }

    private bool ShouldEnvelope(PathString path)
    {
        if (!_options.EnableForAll)
        {
            return false;
        }

        var pathValue = path.Value;
        if (string.IsNullOrEmpty(pathValue))
        {
            return false;
        }

        // Check exclusion paths first
        foreach (var excludePath in _options.ExcludePaths)
        {
            if (pathValue.StartsWith(excludePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check inclusion paths
        if (_options.IncludePaths.Length > 0)
        {
            foreach (var includePath in _options.IncludePaths)
            {
                if (pathValue.StartsWith(includePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        return true;
    }

    private static bool IsAlreadyEnveloped(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            // Check if the response already has the envelope structure
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("success", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("text/json", StringComparison.OrdinalIgnoreCase);
    }

    private static string WrapSuccessResponse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var data = document.RootElement;

            var envelope = new
            {
                success = true,
                data = data,
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
        catch (JsonException)
        {
            // If the body isn't valid JSON, wrap as a string
            var envelope = new
            {
                success = true,
                data = responseBody,
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
    }

    private static string WrapErrorResponse(string responseBody, int statusCode, string traceId)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            // Try to extract existing error info
            var code = root.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : MapStatusCodeToErrorCode(statusCode);
            var message = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : MapStatusCodeToMessage(statusCode);

            var envelope = new ApiErrorResponse(
                code ?? MapStatusCodeToErrorCode(statusCode),
                message ?? MapStatusCodeToMessage(statusCode))
            {
                TraceId = traceId,
                Details = root.TryGetProperty("details", out var detailsElement)
                    ? detailsElement
                    : null
            };

            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
        catch (JsonException)
        {
            var envelope = new ApiErrorResponse(
                MapStatusCodeToErrorCode(statusCode),
                MapStatusCodeToMessage(statusCode))
            {
                TraceId = traceId
            };

            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
    }

    private static string MapStatusCodeToErrorCode(int statusCode) => statusCode switch
    {
        400 => ErrorCodes.BadRequest,
        401 => ErrorCodes.Unauthorized,
        403 => ErrorCodes.Forbidden,
        404 => ErrorCodes.NotFound,
        405 => ErrorCodes.MethodNotAllowed,
        409 => ErrorCodes.Conflict,
        415 => ErrorCodes.UnsupportedMediaType,
        429 => ErrorCodes.RateLimitExceeded,
        500 => ErrorCodes.InternalServerError,
        503 => ErrorCodes.ServiceUnavailable,
        _ => ErrorCodes.UnknownError
    };

    private static string MapStatusCodeToMessage(int statusCode) => statusCode switch
    {
        400 => "The request was invalid.",
        401 => "Authentication is required.",
        403 => "You do not have permission to access this resource.",
        404 => "The requested resource was not found.",
        405 => "The HTTP method is not allowed for this endpoint.",
        409 => "A conflict occurred with the current state of the resource.",
        415 => "The media type is not supported.",
        429 => "Too many requests. Please try again later.",
        500 => "An internal server error occurred.",
        503 => "The service is temporarily unavailable.",
        _ => "An unexpected error occurred."
    };

    private static async Task WriteToResponseAsync(HttpContext context, string content)
    {
        context.Response.ContentLength = null;
        await context.Response.WriteAsync(content);
    }
}

/// <summary>
/// Extension methods for response envelope configuration.
/// </summary>
public static class ResponseEnvelopeExtensions
{
    /// <summary>
    /// Adds the response envelope middleware to the pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="configure">Optional action to configure response envelope options.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseResponseEnvelope(
        this WebApplication app,
        Action<ResponseEnvelopeOptions>? configure = null)
    {
        var options = new ResponseEnvelopeOptions();
        configure?.Invoke(options);
        app.UseMiddleware<ResponseEnvelopeMiddleware>(options);
        return app;
    }
}
