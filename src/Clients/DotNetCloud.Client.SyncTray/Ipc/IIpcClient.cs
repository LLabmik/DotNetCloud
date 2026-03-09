using DotNetCloud.Client.SyncService.Ipc;

namespace DotNetCloud.Client.SyncTray.Ipc;

/// <summary>
/// Client-side abstraction for communicating with the background
/// <c>DotNetCloud.Client.SyncService</c> over Named Pipe (Windows) or
/// Unix domain socket (Linux).
/// </summary>
public interface IIpcClient
{
    /// <summary>Raised when real-time sync progress data is received for a context.</summary>
    event EventHandler<SyncProgressEventData>? SyncProgressReceived;

    /// <summary>Raised when a sync pass completes for a context.</summary>
    event EventHandler<SyncCompleteEventData>? SyncCompleteReceived;

    /// <summary>Raised when a sync error occurs for a context.</summary>
    event EventHandler<SyncErrorEventData>? SyncErrorReceived;

    /// <summary>Raised when a file conflict is detected and a conflict copy is created.</summary>
    event EventHandler<SyncConflictEventData>? ConflictDetected;

    /// <summary>Raised when per-file transfer progress is received for a context.</summary>
    event EventHandler<TransferProgressEventData>? TransferProgressReceived;

    /// <summary>Raised when a file transfer completes for a context.</summary>
    event EventHandler<TransferCompleteEventData>? TransferCompleteReceived;

    /// <summary>
    /// Raised when the connection state to the SyncService changes
    /// (e.g., connected → disconnected or vice-versa).
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>Gets a value indicating whether the client is currently connected to SyncService.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Opens the connection to SyncService, subscribes to push events, and reconnects
    /// automatically on disconnection until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Lists all sync contexts registered in the SyncService.</summary>
    Task<IReadOnlyList<ContextInfo>> ListContextsAsync(CancellationToken cancellationToken = default);

    /// <summary>Triggers an immediate sync pass for the specified context.</summary>
    Task SyncNowAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Pauses automatic sync for the specified context.</summary>
    Task PauseAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Resumes automatic sync for the specified context.</summary>
    Task ResumeAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Removes an account and stops its sync engine.</summary>
    Task RemoveAccountAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Registers a new account with the SyncService using the supplied OAuth2 tokens.</summary>
    Task AddAccountAsync(AddAccountData data, CancellationToken cancellationToken = default);
}

/// <summary>Event data for <see cref="IIpcClient.SyncProgressReceived"/>.</summary>
public sealed class SyncProgressEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>Current sync state string (e.g. <c>Syncing</c>).</summary>
    public required string State { get; init; }

    /// <summary>Number of files pending upload.</summary>
    public int PendingUploads { get; init; }

    /// <summary>Number of files pending download.</summary>
    public int PendingDownloads { get; init; }
}

/// <summary>Event data for <see cref="IIpcClient.SyncCompleteReceived"/>.</summary>
public sealed class SyncCompleteEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>UTC timestamp of the completed sync pass.</summary>
    public DateTime? LastSyncedAt { get; init; }

    /// <summary>Number of unresolved conflicts.</summary>
    public int Conflicts { get; init; }
}

/// <summary>Event data for <see cref="IIpcClient.SyncErrorReceived"/>.</summary>
public sealed class SyncErrorEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>Human-readable error description.</summary>
    public required string Error { get; init; }
}

/// <summary>Event data for <see cref="IIpcClient.ConflictDetected"/>.</summary>
public sealed class SyncConflictEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>Relative path of the original file.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>Relative path of the conflict copy.</summary>
    public required string ConflictCopyPath { get; init; }
}

/// <summary>Event data for <see cref="IIpcClient.TransferProgressReceived"/>.</summary>
public sealed class TransferProgressEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>File name (leaf only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Bytes transferred so far.</summary>
    public long BytesTransferred { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Chunks completed so far.</summary>
    public int ChunksCompleted { get; init; }

    /// <summary>Total number of chunks.</summary>
    public int ChunksTotal { get; init; }

    /// <summary>Percentage complete (0–100).</summary>
    public double PercentComplete { get; init; }
}

/// <summary>Event data for <see cref="IIpcClient.TransferCompleteReceived"/>.</summary>
public sealed class TransferCompleteEventData
{
    /// <summary>Context this event relates to.</summary>
    public Guid ContextId { get; init; }

    /// <summary>File name (leaf only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Total bytes transferred.</summary>
    public long TotalBytes { get; init; }
}
