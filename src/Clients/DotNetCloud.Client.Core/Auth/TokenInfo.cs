namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Stores access and refresh tokens for an account.
/// </summary>
public sealed class TokenInfo
{
    /// <summary>OAuth2 access token.</summary>
    public required string AccessToken { get; set; }

    /// <summary>OAuth2 refresh token (null if not issued).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>UTC expiry time of the access token.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Whether the access token is currently valid.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Whether a refresh token is available.</summary>
    public bool CanRefresh => RefreshToken is not null;
}
