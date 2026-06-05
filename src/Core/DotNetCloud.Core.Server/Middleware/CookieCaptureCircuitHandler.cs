using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Captures the auth cookie from the initial HTTP request into the scoped
/// <see cref="CookieCaptureStore"/> when a Blazor Server circuit is established.
/// This runs in the circuit scope where <see cref="IHttpContextAccessor"/> is available.
/// </summary>
internal sealed class CookieCaptureCircuitHandler : CircuitHandler
{
    private readonly CookieCaptureStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieCaptureCircuitHandler(CookieCaptureStore store, IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var cookie = _httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie))
        {
            _store.CookieHeader = cookie;
        }
        return Task.CompletedTask;
    }
}
