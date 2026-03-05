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
