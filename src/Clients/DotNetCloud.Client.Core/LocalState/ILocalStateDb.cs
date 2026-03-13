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

    /// <summary>Returns all tracked file records for a context. Used by the local directory scan for bulk comparison.</summary>
    Task<IReadOnlyList<LocalFileRecord>> GetAllFileRecordsAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of local paths that already have a pending upload operation queued
    /// (including deferred/retrying ones). Used to avoid double-queueing during local scans.
    /// </summary>
    Task<IReadOnlySet<string>> GetPendingUploadPathsAsync(string dbPath, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Returns true when a download operation for the same node/path recently failed with
    /// a terminal 404/not-found style error and should not be re-queued immediately.
    /// </summary>
    Task<bool> HasRecentTerminalDownloadFailureAsync(string dbPath, Guid nodeId, string localPath, CancellationToken cancellationToken = default);

    // ── Sync Checkpoint ─────────────────────────────────────────────────────

    /// <summary>Gets the last sync checkpoint timestamp. Returns null if never synced.</summary>
    Task<DateTime?> GetCheckpointAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Updates the last sync checkpoint timestamp (for UI display).</summary>
    Task UpdateCheckpointAsync(string dbPath, DateTime checkpoint, CancellationToken cancellationToken = default);

    /// <summary>Gets the stored server-issued sync cursor. Returns null if no cursor yet (triggers full sync on next call).</summary>
    Task<string?> GetSyncCursorAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Persists the server-issued sync cursor after each page of changes for crash resilience.</summary>
    Task UpdateSyncCursorAsync(string dbPath, string cursor, CancellationToken cancellationToken = default);

    // ── Active Upload Sessions ──────────────────────────────────────────────

    /// <summary>Saves a new active upload session record after <c>InitiateUploadAsync</c> succeeds.</summary>
    Task SaveActiveUploadSessionAsync(string dbPath, ActiveUploadSessionRecord record, CancellationToken cancellationToken = default);

    /// <summary>Updates the set of successfully uploaded chunk hashes for an active session.</summary>
    Task UpdateActiveUploadSessionChunksAsync(string dbPath, Guid sessionId, IReadOnlyList<string> uploadedChunkHashes, CancellationToken cancellationToken = default);

    /// <summary>Deletes an active upload session record after <c>CompleteUploadAsync</c> succeeds.</summary>
    Task DeleteActiveUploadSessionAsync(string dbPath, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Gets all active (incomplete) upload sessions.</summary>
    Task<IReadOnlyList<ActiveUploadSessionRecord>> GetActiveUploadSessionsAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Deletes upload session records created before the given timestamp (stale session cleanup).</summary>
    Task DeleteStaleActiveUploadSessionsAsync(string dbPath, DateTime olderThan, CancellationToken cancellationToken = default);

    // ── Conflict Records ────────────────────────────────────────────────────

    /// <summary>Inserts a new conflict record (detected or auto-resolved).</summary>
    Task SaveConflictRecordAsync(string dbPath, ConflictRecord record, CancellationToken cancellationToken = default);

    /// <summary>Returns all conflict records that have not yet been resolved.</summary>
    Task<IReadOnlyList<ConflictRecord>> GetUnresolvedConflictsAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>Returns conflict records detected or resolved within the last 30 days.</summary>
    Task<IReadOnlyList<ConflictRecord>> GetConflictHistoryAsync(string dbPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the conflict with <paramref name="conflictId"/> as resolved with the given
    /// <paramref name="resolution"/> string, setting <c>ResolvedAt</c> to UTC now.
    /// </summary>
    Task ResolveConflictAsync(string dbPath, int conflictId, string resolution, CancellationToken cancellationToken = default);
}
