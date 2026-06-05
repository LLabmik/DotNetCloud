using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Middleware that captures the auth cookie from the incoming HTTP request
/// and stores it in the scoped <see cref="CookieCaptureStore"/>.
/// Must run early in the pipeline so the cookie is available to Blazor components
/// rendered during the same request scope.
/// </summary>
internal sealed class CookieCaptureMiddleware
{
    private readonly RequestDelegate _next;

    public CookieCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var store = context.RequestServices.GetRequiredService<CookieCaptureStore>();
        var cookie = context.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie))
        {
            store.CookieHeader = cookie;
        }
        // Do NOT clear the store when there's no cookie — a previous request
        // in the same scope (e.g., the initial page load) may have set it.

        return _next(context);
    }
}
