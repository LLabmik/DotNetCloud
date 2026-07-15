namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Initiates and completes an OAuth2/OIDC login flow.
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Launches the system browser to the authorization endpoint and waits for the redirect callback.
    /// Returns the access and refresh tokens on success.
    /// </summary>
    /// <param name="serverBaseUrl">Base URL of the DotNetCloud server.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OAuth2Result> AuthenticateAsync(string serverBaseUrl, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an expired access token using the stored refresh token.
    /// </summary>
    /// <param name="serverBaseUrl">Base URL of the DotNetCloud server.</param>
    /// <param name="refreshToken">Existing refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OAuth2Result> RefreshAsync(string serverBaseUrl, string refreshToken, CancellationToken ct = default);
}

/// <summary>Result from a completed OAuth2 authentication or token refresh.</summary>
/// <param name="AccessToken">The new access token.</param>
/// <param name="RefreshToken">The new refresh token.</param>
/// <param name="ExpiresAt">UTC time when the access token expires.</param>
public sealed record OAuth2Result(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
