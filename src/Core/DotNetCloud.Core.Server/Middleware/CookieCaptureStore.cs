namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Scoped service that stores the auth cookie captured from the initial HTTP request.
/// The cookie is written by <see cref="CookieCaptureMiddleware"/> during the initial
/// HTTP request (when HttpContext is available) and read by <see cref="CookieForwardingHandler"/>
/// during subsequent Blazor Server SignalR events (when HttpContext is null).
/// </summary>
internal sealed class CookieCaptureStore
{
    /// <summary>
    /// The captured Cookie header value from the initial HTTP request, or null if not yet captured.
    /// </summary>
    public string? CookieHeader { get; set; }
}
