using DotNetCloud.Client.Core.Transfer;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Drives bidirectional file synchronization for a <see cref="SyncContext"/>.
/// </summary>
public interface ISyncEngine : IAsyncDisposable
{
    /// <summary>Raised when the sync status changes.</summary>
    event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

    /// <summary>Raised when progress is reported for an individual file transfer (upload or download).</summary>
    event EventHandler<FileTransferProgressEventArgs>? FileTransferProgress;

    /// <summary>Raised when an individual file transfer completes.</summary>
    event EventHandler<FileTransferCompleteEventArgs>? FileTransferComplete;

    /// <summary>
    /// Whether the folder size limit is active. When enabled, folders whose recursive total
    /// size exceeds <see cref="MaxFolderSizeBytes"/> are excluded from sync after a one-time
    /// per-folder prompt. Applied at the start of each sync pass.
    /// </summary>
    bool SizeLimitEnabled { get; set; }

    /// <summary>Maximum recursive folder size (bytes) before a folder is considered over-limit. Default 250 MiB.</summary>
    long MaxFolderSizeBytes { get; set; }

    /// <summary>Raised when a folder exceeds the size limit and no decision has been recorded yet.</summary>
    event EventHandler<SizeLimitDecisionRequestedEventArgs>? SizeLimitDecisionRequested;

    /// <summary>
    /// Starts the sync engine (enables FileSystemWatcher and periodic scan).
    /// </summary>
    Task StartAsync(SyncContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a full bidirectional sync pass immediately.
    /// </summary>
    Task SyncAsync(SyncContext context, CancellationToken cancellationToken = default);

    /// <summary>Returns the current sync status for the given context.</summary>
    Task<SyncStatus> GetStatusAsync(SyncContext context, CancellationToken cancellationToken = default);

    /// <summary>Pauses automatic sync (FileSystemWatcher events are queued).</summary>
    Task PauseAsync(SyncContext context, CancellationToken cancellationToken = default);

    /// <summary>Resumes automatic sync.</summary>
    Task ResumeAsync(SyncContext context, CancellationToken cancellationToken = default);

    /// <summary>Stops the sync engine and releases all resources.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments for a sync status change.
/// </summary>
public sealed class SyncStatusChangedEventArgs : EventArgs
{
    /// <summary>The updated sync status.</summary>
    public required SyncStatus Status { get; init; }

    /// <summary>The sync context the status belongs to.</summary>
    public required SyncContext Context { get; init; }
}

/// <summary>Event arguments for per-file transfer progress.</summary>
public sealed class FileTransferProgressEventArgs : EventArgs
{
    /// <summary>File name (leaf name only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Progress snapshot at the time of the event.</summary>
    public required TransferProgress Progress { get; init; }
}

/// <summary>Event arguments raised when a per-file transfer finishes.</summary>
public sealed class FileTransferCompleteEventArgs : EventArgs
{
    /// <summary>File name (leaf name only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Total bytes transferred.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Total chunks transferred.</summary>
    public int TotalChunks { get; init; }
}

/// <summary>
/// Raised when a folder's recursive total size exceeds the folder size limit and the user has
/// not yet decided whether to sync it. When forwarded by the sync context manager,
/// <see cref="ContextId"/> identifies the sync context.
/// </summary>
public sealed class SizeLimitDecisionRequestedEventArgs : EventArgs
{
    /// <summary>The sync context ID (set when forwarded by the context manager).</summary>
    public Guid? ContextId { get; init; }

    /// <summary>Folder path relative to the sync root (forward slashes, no leading slash).</summary>
    public required string RelativePath { get; init; }

    /// <summary>The folder's recursive total size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>The configured size limit in bytes.</summary>
    public long LimitBytes { get; init; }
}
