namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// A snapshot of the current synchronization status for a context.
/// </summary>
public sealed class SyncStatus
{
    /// <summary>Current operational state.</summary>
    public SyncState State { get; init; }

    /// <summary>Number of files currently pending upload.</summary>
    public int PendingUploads { get; init; }

    /// <summary>Number of files currently pending download.</summary>
    public int PendingDownloads { get; init; }

    /// <summary>Number of unresolved conflicts.</summary>
    public int Conflicts { get; init; }

    /// <summary>Timestamp of the last successful sync.</summary>
    public DateTime? LastSyncedAt { get; init; }

    /// <summary>Last error message (null if no error).</summary>
    public string? LastError { get; init; }

    /// <summary>Bytes uploaded in the current sync pass.</summary>
    public long BytesUploaded { get; init; }

    /// <summary>Bytes downloaded in the current sync pass.</summary>
    public long BytesDownloaded { get; init; }
}
