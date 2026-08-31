using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Applies <c>Cache-Control: no-store</c> to all API responses so browsers never cache
/// user-specific data, preventing stale cross-session data from being served from the
/// HTTP cache (e.g., one user seeing another user's profile after an account switch).
/// </summary>
/// <remarks>
/// Raw media/stream endpoints are excluded so video, music, and file downloads keep their
/// cacheability for smooth playback and seeking. The excluded prefixes mirror the paths
/// the response-envelope middleware skips (raw binary content that must not be buffered).
/// </remarks>
public sealed class ApiNoCacheMiddleware
{
    // Media/raw-content endpoints that must stay cacheable for streaming performance.
    private static readonly string[] ExcludedPathPrefixes =
    [
        "/api/v1/videos/",
        "/api/v1/music/",
        "/api/v1/files/",
        "/api/v1/wopi/files/",
    ];

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiNoCacheMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public ApiNoCacheMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware, applying no-store headers to cacheable API responses.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(path) &&
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            !IsExcludedMediaPath(path))
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
        }

        await _next(context);
    }

    private static bool IsExcludedMediaPath(string path)
    {
        foreach (var prefix in ExcludedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
