using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Forwards authentication cookies from the incoming HTTP request to outgoing
/// HttpClient requests. This enables server-side rendered Blazor components to
/// make authenticated API calls to the same origin.
/// </summary>
internal sealed class CookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var cookieHeader = httpContext.Request.Headers.Cookie.ToString();
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
