using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Forwards authentication cookies to outgoing HttpClient requests.
/// Tries multiple sources in order: the scoped <see cref="CookieCaptureStore"/> 
/// (populated by Blazor circuit initialization), then <see cref="IHttpContextAccessor"/>
/// for direct HTTP requests.
/// </summary>
internal sealed class CookieForwardingHandler : DelegatingHandler
{
    private readonly CookieCaptureStore _cookieStore;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieForwardingHandler(CookieCaptureStore cookieStore, IHttpContextAccessor httpContextAccessor)
    {
        _cookieStore = cookieStore;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Prefer the scoped store (set during Blazor circuit init)
        var cookie = _cookieStore.CookieHeader;

        // Fall back to current HTTP context (for non-Blazor requests)
        if (string.IsNullOrEmpty(cookie))
        {
            cookie = _httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
        }

        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
