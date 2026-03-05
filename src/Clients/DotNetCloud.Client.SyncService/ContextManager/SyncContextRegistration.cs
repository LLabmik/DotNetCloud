namespace DotNetCloud.Client.SyncService.ContextManager;

/// <summary>
/// Persisted metadata for a sync context, identifying one OS-user + server-account pair.
/// One instance exists per (local OS user, DotNetCloud account) combination.
/// </summary>
public sealed class SyncContextRegistration
{
    /// <summary>Unique identifier for this sync context.</summary>
    public required Guid Id { get; init; }

    /// <summary>Base URL of the DotNetCloud server (e.g. <c>https://cloud.example.com</c>).</summary>
    public required string ServerBaseUrl { get; init; }

    /// <summary>Authenticated user ID on the server.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Absolute path of the local folder to synchronise.</summary>
    public required string LocalFolderPath { get; init; }

    /// <summary>
    /// Human-readable name shown in the tray UI (e.g. <c>Ben @ cloud.example.com</c>).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>Key used by <c>ITokenStore</c> to look up OAuth2 tokens for this context.</summary>
    public required string AccountKey { get; init; }

    /// <summary>
    /// OS username of the account owner.
    /// Used for privilege-dropping on Linux and impersonation on Windows.
    /// </summary>
    public required string OsUserName { get; init; }

    /// <summary>
    /// Per-context data directory containing the SQLite state DB,
    /// encrypted token files, and selective-sync config.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>Interval between periodic full-scan passes (default 5 minutes).</summary>
    public TimeSpan FullScanInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>UTC timestamp when this context was first registered.</summary>
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
}
