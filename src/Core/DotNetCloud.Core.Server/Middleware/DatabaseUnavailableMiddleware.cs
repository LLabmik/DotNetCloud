using System.Text.Json;
using DotNetCloud.Core.Server.Services;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Returns HTTP 503 quickly for database-dependent requests while the database
/// is unavailable, instead of letting them block on connection timeouts.
/// Health, metrics, root-CA and static asset paths are served normally.
/// </summary>
public sealed class DatabaseUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DatabaseConnectivityState _state;

    private static readonly HashSet<string> AllowlistedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",      // /health, /health/live, /health/ready
        "/metrics",     // Prometheus scrape endpoint
        "/root-ca.crt", // self-signed CA download
        "/_framework",  // Blazor static assets
        "/favicon.ico",
    };

    /// <summary>Creates the middleware with the given connectivity state.</summary>
    public DatabaseUnavailableMiddleware(
        RequestDelegate next,
        DatabaseConnectivityState state)
    {
        _next = next;
        _state = state;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_state.IsAvailable && RequiresDatabase(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = "5";

            var payload = JsonSerializer.Serialize(new
            {
                success = false,
                code = "DATABASE_UNAVAILABLE",
                message = "The server database is temporarily unavailable. Please try again shortly."
            });

            await context.Response.WriteAsync(payload, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool RequiresDatabase(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in AllowlistedPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
