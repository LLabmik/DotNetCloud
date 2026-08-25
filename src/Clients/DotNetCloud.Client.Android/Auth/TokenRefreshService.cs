using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Proactive access-token refresh. Persists the token expiry and refreshes via the
/// OAuth2 <c>refresh_token</c> grant before the access token expires, so the client
/// never holds an expired token. A single lock serializes concurrent refreshes.
/// This service never navigates or logs out on failure — callers decide what to do
/// with a <c>null</c> result.
/// </summary>
internal sealed class TokenRefreshService : ITokenRefreshService
{
    private readonly ISecureTokenStore _tokenStore;
    private readonly IOAuth2Service _oauth;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>How long before expiry the access token is considered stale and refreshed.</summary>
    internal static readonly TimeSpan SafetyWindow = TimeSpan.FromMinutes(5);

    /// <summary>Initializes a new <see cref="TokenRefreshService"/>.</summary>
    public TokenRefreshService(
        ISecureTokenStore tokenStore,
        IOAuth2Service oauth,
        ILogger<TokenRefreshService> logger)
    {
        _tokenStore = tokenStore;
        _oauth = oauth;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> EnsureFreshAccessTokenAsync(string serverUrl, CancellationToken ct = default, bool forceRefresh = false)
    {
        var currentToken = await _tokenStore.GetAccessTokenAsync(serverUrl, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(currentToken))
            return null;

        if (!forceRefresh)
        {
            var expiry = await _tokenStore.GetAccessTokenExpiryAsync(serverUrl, ct).ConfigureAwait(false);
            // Unknown expiry (tokens saved by an older app version) — assume fresh and
            // let the reactive 401 path force a refresh if the server disagrees.
            if (expiry is null || DateTimeOffset.UtcNow < expiry.Value - SafetyWindow)
                return currentToken;
        }

        return await RefreshUnderLockAsync(serverUrl, ct, forceRefresh).ConfigureAwait(false);
    }

    private async Task<string?> RefreshUnderLockAsync(string serverUrl, CancellationToken ct, bool forceRefresh)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // A concurrent caller may have refreshed while we waited — reuse it unless forced.
            if (!forceRefresh)
            {
                var currentToken = await _tokenStore.GetAccessTokenAsync(serverUrl, ct).ConfigureAwait(false);
                var expiry = await _tokenStore.GetAccessTokenExpiryAsync(serverUrl, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(currentToken) &&
                    expiry is not null &&
                    DateTimeOffset.UtcNow < expiry.Value - SafetyWindow)
                {
                    return currentToken;
                }
            }

            var refreshToken = await _tokenStore.GetRefreshTokenAsync(serverUrl, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger.LogWarning("No refresh token available for {ServerUrl}; cannot refresh.", serverUrl);
                return null;
            }

            var result = await _oauth.RefreshAsync(serverUrl, refreshToken, ct).ConfigureAwait(false);
            await _tokenStore.SaveTokensAsync(
                serverUrl, result.AccessToken, result.RefreshToken, result.IdToken, result.ExpiresAt, ct).ConfigureAwait(false);
            _logger.LogInformation("Proactively refreshed access token for {ServerUrl}.", serverUrl);
            return result.AccessToken;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Proactive token refresh failed for {ServerUrl}.", serverUrl);
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
