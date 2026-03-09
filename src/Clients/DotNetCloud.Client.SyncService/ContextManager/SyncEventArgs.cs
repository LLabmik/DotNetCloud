using DotNetCloud.Client.Core.Sync;

namespace DotNetCloud.Client.SyncService.ContextManager;

/// <summary>Event arguments raised when a sync pass is in progress.</summary>
public sealed class SyncProgressEventArgs : EventArgs
{
    /// <summary>Identifier of the context reporting progress.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>Current sync status snapshot.</summary>
    public required SyncStatus Status { get; init; }
}

/// <summary>Event arguments raised when a sync pass completes successfully.</summary>
public sealed class SyncCompleteEventArgs : EventArgs
{
    /// <summary>Identifier of the context that completed a sync pass.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>Final sync status after the pass.</summary>
    public required SyncStatus Status { get; init; }
}

/// <summary>Event arguments raised when a sync error occurs.</summary>
public sealed class SyncErrorEventArgs : EventArgs
{
    /// <summary>Identifier of the context that encountered an error.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string ErrorMessage { get; init; }
}

/// <summary>Event arguments raised when a sync conflict is detected.</summary>
public sealed class SyncConflictDetectedEventArgs : EventArgs
{
    /// <summary>Identifier of the context in which the conflict occurred.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>Path of the original (remote) file.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>Path of the local conflict copy that was created.</summary>
    public required string ConflictCopyPath { get; init; }
}

/// <summary>Event arguments raised when progress is reported for an individual file transfer.</summary>
public sealed class ContextTransferProgressEventArgs : EventArgs
{
    /// <summary>Identifier of the context in which the transfer is occurring.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>File name (leaf only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Bytes transferred so far.</summary>
    public long BytesTransferred { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Chunks completed so far.</summary>
    public int ChunksTransferred { get; init; }

    /// <summary>Total number of chunks.</summary>
    public int TotalChunks { get; init; }

    /// <summary>Percentage complete (0–100).</summary>
    public double PercentComplete { get; init; }
}

/// <summary>Event arguments raised when an individual file transfer completes.</summary>
public sealed class ContextTransferCompleteEventArgs : EventArgs
{
    /// <summary>Identifier of the context in which the transfer completed.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>File name (leaf only).</summary>
    public required string FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public required string Direction { get; init; }

    /// <summary>Total bytes transferred.</summary>
    public long TotalBytes { get; init; }
}

/// <summary>Event arguments raised when a sync conflict is auto-resolved without user intervention.</summary>
public sealed class SyncConflictAutoResolvedEventArgs : EventArgs
{
    /// <summary>Identifier of the context in which the conflict was resolved.</summary>
    public required Guid ContextId { get; init; }

    /// <summary>Local path of the file that was auto-resolved.</summary>
    public required string LocalPath { get; init; }

    /// <summary>Name of the auto-resolution strategy that succeeded.</summary>
    public required string Strategy { get; init; }

    /// <summary>Human-readable description of the chosen resolution.</summary>
    public required string Resolution { get; init; }
}
