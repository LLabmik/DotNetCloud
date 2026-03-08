namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// A count of pending operations in the local state database.
/// </summary>
public sealed class PendingOperationCount
{
    /// <summary>Number of pending uploads.</summary>
    public int Uploads { get; init; }

    /// <summary>Number of pending downloads.</summary>
    public int Downloads { get; init; }

    /// <summary>Number of unresolved conflicts.</summary>
    public int Conflicts { get; init; }
}

/// <summary>
/// Manages the per-context SQLite state database that tracks local file sync state,
/// pending operations, sync checkpoints, and account configuration.
/// </summary>
public interface ILocalStateDb
{
    /// <summary>Initializes (creates if needed) the database at the given path. Runs integrity check and recovers from corruption if detected.</summary>
    Task InitializeAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Returns true if the local state database at the given path was reset due to corruption during the last call to <see cref="InitializeAsync"/>.</summary>
    bool WasRecentlyReset(string dbPath);

    /// <summary>Checkpoints the SQLite WAL file to truncate it after a sync pass.</summary>
    Task CheckpointWalAsync(string dbPath, CancellationToken cancellationToken = default);

    // ── File Records ────────────────────────────────────────────────────────

    /// <summary>Gets the sync record for a local path. Returns null if not tracked.</summary>
    Task<LocalFileRecord?> GetFileRecordAsync(string dbPath, string localPath, CancellationToken cancellationToken = default);

    /// <summary>Gets the sync record by server node ID. Returns null if not found.</summary>
    Task<LocalFileRecord?> GetFileRecordByNodeIdAsync(string dbPath, Guid nodeId, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates a file sync record.</summary>
    Task UpsertFileRecordAsync(string dbPath, LocalFileRecord record, CancellationToken cancellationToken = default);

    /// <summary>Removes a file sync record by local path.</summary>
    Task RemoveFileRecordAsync(string dbPath, string localPath, CancellationToken cancellationToken = default);

    // ── Pending Operations ──────────────────────────────────────────────────

    /// <summary>Queues a pending operation.</summary>
    Task QueueOperationAsync(string dbPath, PendingOperationRecord operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending operations that are eligible to run now, ordered by queue time.
    /// Operations with a future <c>NextRetryAt</c> are excluded until their scheduled time.
    /// </summary>
    Task<IReadOnlyList<PendingOperationRecord>> GetPendingOperationsAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Gets pending operation counts.</summary>
    Task<PendingOperationCount> GetPendingOperationCountAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Removes a completed or cancelled operation.</summary>
    Task RemoveOperationAsync(string dbPath, int operationId, CancellationToken cancellationToken = default);

    /// <summary>Updates retry metadata for a pending operation after a transient failure.</summary>
    Task UpdateOperationRetryAsync(string dbPath, int operationId, int retryCount, DateTime? nextRetryAt, string? lastError, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a permanently-failed operation to the failed operations table and removes it from pending.
    /// Called after <see cref="PendingOperationRecord.RetryCount"/> reaches the maximum.
    /// </summary>
    Task MoveToFailedAsync(string dbPath, PendingOperationRecord operation, string lastError, CancellationToken cancellationToken = default);

    // ── Sync Checkpoint ─────────────────────────────────────────────────────

    /// <summary>Gets the last sync checkpoint timestamp. Returns null if never synced.</summary>
    Task<DateTime?> GetCheckpointAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Updates the last sync checkpoint timestamp.</summary>
    Task UpdateCheckpointAsync(string dbPath, DateTime checkpoint, CancellationToken cancellationToken = default);
}
