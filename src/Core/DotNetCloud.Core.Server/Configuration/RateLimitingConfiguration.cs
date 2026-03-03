using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.RateLimiting;
using DotNetCloud.Core.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Gets or sets whether rate limiting is enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the global rate limit (requests per window). Defaults to 100.
    /// </summary>
    public int GlobalPermitLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the global rate limit window in seconds. Defaults to 60.
    /// </summary>
    public int GlobalWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the authenticated user rate limit (requests per window). Defaults to 200.
    /// </summary>
    public int AuthenticatedPermitLimit { get; set; } = 200;

    /// <summary>
    /// Gets or sets the authenticated user rate limit window in seconds. Defaults to 60.
    /// </summary>
    public int AuthenticatedWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets per-module rate limits (module name → permits per window).
    /// </summary>
    public Dictionary<string, ModuleRateLimitConfig> ModuleLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to include rate limit headers in responses. Defaults to true.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the queue limit for requests that exceed the rate limit. Defaults to 0 (no queueing).
    /// </summary>
    public int QueueLimit { get; set; } = 0;
}

/// <summary>
/// Rate limit configuration for a specific module.
/// </summary>
public sealed class ModuleRateLimitConfig
{
    /// <summary>
    /// Gets or sets the permit limit for the module.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the window in seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Extension methods for configuring rate limiting.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// The name of the global fixed window rate limiter policy.
    /// </summary>
    public const string GlobalPolicy = "global";

    /// <summary>
    /// The name of the authenticated user rate limiter policy.
    /// </summary>
    public const string AuthenticatedPolicy = "authenticated";

    /// <summary>
    /// Adds DotNetCloud rate limiting services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new RateLimitingOptions();
        configuration.GetSection(RateLimitingOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        if (!options.Enabled)
        {
            return services;
        }

        services.AddRateLimiter(limiterOptions =>
        {
            // Global fixed window rate limiter (per IP)
            limiterOptions.AddPolicy(GlobalPolicy, context =>
            {
                var clientIp = GetClientIpAddress(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.GlobalPermitLimit,
                        Window = TimeSpan.FromSeconds(options.GlobalWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = options.QueueLimit
                    });
            });

            // Authenticated user rate limiter (per user)
            limiterOptions.AddPolicy(AuthenticatedPolicy, context =>
            {
                var userId = context.User?.FindFirst("sub")?.Value ?? GetClientIpAddress(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.AuthenticatedPermitLimit,
                        Window = TimeSpan.FromSeconds(options.AuthenticatedWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = options.QueueLimit
                    });
            });

            // Per-module rate limiters
            foreach (var (moduleName, moduleConfig) in options.ModuleLimits)
            {
                limiterOptions.AddPolicy($"module-{moduleName}", context =>
                {
                    var userId = context.User?.FindFirst("sub")?.Value ?? GetClientIpAddress(context);
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"{moduleName}:{userId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = moduleConfig.PermitLimit,
                            Window = TimeSpan.FromSeconds(moduleConfig.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        });
                });
            }

            // Configure rejection response
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue
                    : TimeSpan.FromSeconds(options.GlobalWindowSeconds);

                if (options.IncludeHeaders)
                {
                    context.HttpContext.Response.Headers["Retry-After"] =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var errorResponse = new ApiErrorResponse(
                    ErrorCodes.RateLimitExceeded,
                    $"Rate limit exceeded. Please retry after {(int)retryAfter.TotalSeconds} seconds.")
                {
                    TraceId = context.HttpContext.TraceIdentifier
                };

                await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Adds rate limit headers to responses using a middleware.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseDotNetCloudRateLimiting(this WebApplication app)
    {
        var options = app.Services.GetService<RateLimitingOptions>();
        if (options is null || !options.Enabled)
        {
            return app;
        }

        app.UseRateLimiter();
        return app;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (reverse proxy scenario)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP which is the client's original IP
            var firstIp = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return firstIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
