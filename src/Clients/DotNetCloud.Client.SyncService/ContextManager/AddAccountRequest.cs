namespace DotNetCloud.Client.SyncService.ContextManager;

/// <summary>
/// Request to add a new sync account (context) to the running service.
/// Typically populated from the IPC <c>add-account</c> command after the user
/// completes the OAuth2 PKCE flow in the tray application.
/// </summary>
public sealed class AddAccountRequest
{
    /// <summary>Base URL of the DotNetCloud server.</summary>
    public required string ServerBaseUrl { get; init; }

    /// <summary>Authenticated user ID on the server.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Absolute path of the local folder to synchronise.</summary>
    public required string LocalFolderPath { get; init; }

    /// <summary>Human-readable display name for the tray UI.</summary>
    public required string DisplayName { get; init; }

    /// <summary>OAuth2 access token obtained after authorisation.</summary>
    public required string AccessToken { get; init; }

    /// <summary>OAuth2 refresh token (may be null for short-lived grants).</summary>
    public string? RefreshToken { get; init; }

    /// <summary>UTC expiry time of the access token.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>OS username of the account owner (defaults to the current user).</summary>
    public string OsUserName { get; init; } = Environment.UserName;

    /// <summary>Interval between periodic full-scan passes (default 5 minutes).</summary>
    public TimeSpan FullScanInterval { get; init; } = TimeSpan.FromMinutes(5);
}
