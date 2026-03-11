namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Stores and retrieves OAuth2 access and refresh tokens backed by the platform secure storage.
/// </summary>
public interface ISecureTokenStore
{
    /// <summary>Saves tokens for a server connection.</summary>
    Task SaveTokensAsync(string serverUrl, string accessToken, string refreshToken, CancellationToken ct = default);

    /// <summary>Retrieves the stored access token for a server, or <c>null</c> if not found.</summary>
    Task<string?> GetAccessTokenAsync(string serverUrl, CancellationToken ct = default);

    /// <summary>Retrieves the stored refresh token for a server, or <c>null</c> if not found.</summary>
    Task<string?> GetRefreshTokenAsync(string serverUrl, CancellationToken ct = default);

    /// <summary>Deletes stored tokens for a server (logout).</summary>
    Task DeleteTokensAsync(string serverUrl, CancellationToken ct = default);
}
