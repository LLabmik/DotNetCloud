using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Intercepts HTTP 401 responses, attempts to refresh the access token using
/// the stored refresh token, and retries the original request transparently.
/// If the refresh fails, clears stored tokens and navigates to the login page.
/// </summary>
internal sealed class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly ISecureTokenStore _tokenStore;
    private readonly IOAuth2Service _oauth;
    private readonly ILogger<AuthenticatedHttpClientHandler> _logger;
    private static readonly SemaphoreSlim s_refreshLock = new(1, 1);

    /// <summary>Initializes a new <see cref="AuthenticatedHttpClientHandler"/>.</summary>
    public AuthenticatedHttpClientHandler(
        ISecureTokenStore tokenStore,
        IOAuth2Service oauth,
        ILogger<AuthenticatedHttpClientHandler> logger)
    {
        _tokenStore = tokenStore;
        _oauth = oauth;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var serverUrl = ExtractServerBaseUrl(request.RequestUri);
        if (serverUrl is null)
            return response;

        var failedToken = request.Headers.Authorization?.Parameter;

        await s_refreshLock.WaitAsync(ct);
        try
        {
            // Another concurrent call may have already refreshed the token.
            var currentToken = await _tokenStore.GetAccessTokenAsync(serverUrl, ct);
            if (!string.IsNullOrEmpty(currentToken) && currentToken != failedToken)
            {
                _logger.LogDebug("Token already refreshed by another request; retrying.");
                response.Dispose();
                var retry = CreateRetryRequest(request, currentToken);
                return await base.SendAsync(retry, ct);
            }

            var refreshToken = await _tokenStore.GetRefreshTokenAsync(serverUrl, ct);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("No refresh token available for {ServerUrl}; clearing session.", serverUrl);
                await _tokenStore.DeleteTokensAsync(serverUrl, ct);
                await NavigateToLoginAsync();
                return response;
            }

            var result = await _oauth.RefreshAsync(serverUrl, refreshToken, ct);
            await _tokenStore.SaveTokensAsync(serverUrl, result.AccessToken, result.RefreshToken, ct);
            _logger.LogInformation("Access token refreshed for {ServerUrl}.", serverUrl);

            response.Dispose();
            var retryRequest = CreateRetryRequest(request, result.AccessToken);
            return await base.SendAsync(retryRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed for {ServerUrl}; clearing session.", serverUrl);
            try { await _tokenStore.DeleteTokensAsync(serverUrl, ct); }
            catch { /* best-effort cleanup */ }
            await NavigateToLoginAsync();
            return response;
        }
        finally
        {
            s_refreshLock.Release();
        }
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

        return retry;
    }

    private static string? ExtractServerBaseUrl(Uri? uri)
    {
        if (uri is null) return null;
        return $"{uri.Scheme}://{uri.Authority}";
    }

    private static Task NavigateToLoginAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync("//Login"));
    }
}
