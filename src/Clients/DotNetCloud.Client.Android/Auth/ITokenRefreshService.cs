namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// Ensures a fresh (non-expired) access token for a server connection, refreshing
/// proactively before expiry using the stored refresh token.
/// </summary>
public interface ITokenRefreshService
{
    /// <summary>
    /// Returns a valid access token for the given server, proactively refreshing it
    /// when it is expired or within the safety window before expiry.
    /// </summary>
    /// <param name="serverUrl">Base URL of the DotNetCloud server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="forceRefresh">When <c>true</c>, always attempt a refresh (used after a 401).</param>
    /// <returns>The fresh access token, or <c>null</c> if no tokens are stored or the refresh failed.</returns>
    Task<string?> EnsureFreshAccessTokenAsync(string serverUrl, CancellationToken ct = default, bool forceRefresh = false);
}
