namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Drives bidirectional file synchronization for a <see cref="SyncContext"/>.
/// </summary>
public interface ISyncEngine : IAsyncDisposable
{
    /// <summary>Raised when the sync status changes.</summary>
    event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

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
