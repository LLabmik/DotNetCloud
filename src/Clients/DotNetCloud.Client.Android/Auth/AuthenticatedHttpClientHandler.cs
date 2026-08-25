using System.Net;
using System.Net.Http.Headers;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Attaches a fresh Bearer token to every request, proactively refreshing the access
/// token before it expires via <see cref="ITokenRefreshService"/>. As a fallback it
/// also intercepts HTTP 401 responses, refreshes, and retries transparently. If the
/// refresh genuinely fails, clears tokens and the active connection, then navigates
/// to the login page exactly once (no bounce, because the connection is removed).
/// </summary>
internal sealed class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly ISecureTokenStore _tokenStore;
    private readonly ITokenRefreshService _tokenRefresh;
    private readonly IServerConnectionStore _serverStore;
    private readonly ILogger<AuthenticatedHttpClientHandler> _logger;

    /// <summary>Initializes a new <see cref="AuthenticatedHttpClientHandler"/>.</summary>
    public AuthenticatedHttpClientHandler(
        ISecureTokenStore tokenStore,
        ITokenRefreshService tokenRefresh,
        IServerConnectionStore serverStore,
        ILogger<AuthenticatedHttpClientHandler> logger)
    {
        _tokenStore = tokenStore;
        _tokenRefresh = tokenRefresh;
        _serverStore = serverStore;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        // Proactively attach a fresh token so we never send an expired one. The refresh
        // service returns the current token unchanged when it still has > 5 minutes left.
        var serverUrl = ExtractServerBaseUrl(request.RequestUri);
        if (serverUrl is not null)
        {
            var freshToken = await _tokenRefresh.EnsureFreshAccessTokenAsync(serverUrl, ct);
            if (!string.IsNullOrWhiteSpace(freshToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
        }

        var response = await base.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized || serverUrl is null)
            return response;

        var failedToken = request.Headers.Authorization?.Parameter;

        // The token was rejected — force a refresh and retry once. Concurrent calls are
        // serialized inside the shared refresh service.
        var newToken = await _tokenRefresh.EnsureFreshAccessTokenAsync(serverUrl, ct, forceRefresh: true);
        if (!string.IsNullOrWhiteSpace(newToken) && newToken != failedToken)
        {
            response.Dispose();
            var retry = CreateRetryRequest(request, newToken);
            return await base.SendAsync(retry, ct);
        }

        // Refresh genuinely failed (refresh token expired/revoked) — end the session
        // cleanly. We also remove the active connection so the login page doesn't bounce
        // us straight back (which caused repeated "session lost" loops).
        _logger.LogWarning("Token refresh failed for {ServerUrl}; clearing session.", serverUrl);
        await EndSessionAsync(serverUrl, ct);
        return response;
    }

    private static HttpRequestMessage CreateRetryRequest(HttpRequestMessage original, string accessToken)
    {
        var retry = new HttpRequestMessage(original.Method, original.RequestUri);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        foreach (var header in original.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Preserve the request body (POST/PUT/PATCH) so the retried request is complete.
        if (original.Content is not null)
            retry.Content = original.Content;

        return retry;
    }

    private static string? ExtractServerBaseUrl(Uri? uri)
    {
        if (uri is null)
            return null;
        return $"{uri.Scheme}://{uri.Authority}";
    }

    private async Task EndSessionAsync(string serverUrl, CancellationToken ct)
    {
        try { await _tokenStore.DeleteTokensAsync(serverUrl, ct); }
        catch { /* best-effort cleanup */ }

        try { _serverStore.Remove(serverUrl); }
        catch { /* best-effort cleanup */ }

        await NavigateToLoginAsync();
    }

    private static Task NavigateToLoginAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync("//Login"));
    }
}
