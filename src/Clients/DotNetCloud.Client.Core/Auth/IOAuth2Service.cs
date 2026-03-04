namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Manages OAuth2 Authorization Code + PKCE authentication flows.
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Runs the full OAuth2 PKCE browser-based authorization flow.
    /// Opens the system browser, listens for the redirect callback, and returns tokens.
    /// </summary>
    /// <param name="serverBaseUrl">Base URL of the DotNetCloud server (e.g. https://cloud.example.com).</param>
    /// <param name="clientId">OAuth2 client ID.</param>
    /// <param name="scopes">Requested scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Token information.</returns>
    Task<TokenInfo> AuthorizeAsync(string serverBaseUrl, string clientId, IEnumerable<string> scopes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an expired access token using a refresh token.
    /// </summary>
    Task<TokenInfo> RefreshAsync(string serverBaseUrl, string clientId, TokenInfo currentTokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all tokens for an account.
    /// </summary>
    Task RevokeAsync(string serverBaseUrl, string clientId, TokenInfo tokens, CancellationToken cancellationToken = default);
}
