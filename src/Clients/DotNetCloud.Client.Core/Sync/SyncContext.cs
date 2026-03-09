namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Identifies a single sync pairing: one OS user ↔ one server account ↔ one local folder.
/// </summary>
public sealed class SyncContext
{
    /// <summary>Unique identifier for this sync context.</summary>
    public required Guid Id { get; init; }

    /// <summary>Base URL of the DotNetCloud server.</summary>
    public required string ServerBaseUrl { get; init; }

    /// <summary>Authenticated user's ID on the server.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Local file system folder that is synced.</summary>
    public required string LocalFolderPath { get; init; }

    /// <summary>Friendly display name (e.g. "Ben @ cloud.example.com").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Path to the SQLite state database for this context.</summary>
    public required string StateDatabasePath { get; init; }

    /// <summary>Account key used to load tokens from the token store.</summary>
    public required string AccountKey { get; init; }

    /// <summary>Interval for periodic full-scan fallback (default 5 minutes).</summary>
    public TimeSpan FullScanInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Upload bandwidth limit in KB/s. 0 means unlimited.</summary>
    public decimal UploadLimitKbps { get; init; }

    /// <summary>Download bandwidth limit in KB/s. 0 means unlimited.</summary>
    public decimal DownloadLimitKbps { get; init; }
}
