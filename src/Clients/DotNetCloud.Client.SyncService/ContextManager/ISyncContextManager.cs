using DotNetCloud.Client.Core.Sync;

namespace DotNetCloud.Client.SyncService.ContextManager;

/// <summary>
/// Manages active sync contexts — one per OS-user + server-account pair.
/// Orchestrates the lifecycle of per-context <see cref="ISyncEngine"/> instances
/// and provides operations for IPC clients.
/// </summary>
public interface ISyncContextManager
{
    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads persisted context registrations from the system registry file
    /// and starts a sync engine for each. Called once at service startup.
    /// </summary>
    Task LoadContextsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running sync engines. Called during service shutdown.
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);

    // ── Context management ─────────────────────────────────────────────────

    /// <summary>Returns a snapshot of all registered contexts.</summary>
    Task<IReadOnlyList<SyncContextRegistration>> GetContextsAsync();

    /// <summary>
    /// Adds a new sync account, saves its tokens, starts its sync engine,
    /// and persists the registration.
    /// </summary>
    Task<SyncContextRegistration> AddContextAsync(
        AddAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a sync context, stops and disposes its engine, deletes its
    /// stored tokens, and removes it from the persisted registry.
    /// </summary>
    Task RemoveContextAsync(Guid contextId, CancellationToken cancellationToken = default);

    // ── Per-context operations ─────────────────────────────────────────────

    /// <summary>Returns the current sync status for the given context, or <c>null</c> if not found.</summary>
    Task<SyncStatus?> GetStatusAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Pauses automatic sync (FileSystemWatcher + periodic scan) for a context.</summary>
    Task PauseAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Resumes automatic sync for a context and triggers an immediate catch-up pass.</summary>
    Task ResumeAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Triggers an immediate full sync pass for a context.</summary>
    Task SyncNowAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the conflict records for the given context.
    /// When <paramref name="includeHistory"/> is true, returns all records from the last 30 days
    /// (including resolved). Otherwise only unresolved conflicts are returned.
    /// </summary>
    Task<IReadOnlyList<DotNetCloud.Client.Core.LocalState.ConflictRecord>> ListConflictsAsync(
        Guid contextId, bool includeHistory = false, CancellationToken cancellationToken = default);

    /// <summary>Marks a conflict record as resolved with the given resolution string.</summary>
    Task ResolveConflictAsync(
        Guid contextId, int conflictId, string resolution,
        CancellationToken cancellationToken = default);

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>Raised when a sync pass is in progress for any context.</summary>
    event EventHandler<SyncProgressEventArgs>? SyncProgress;

    /// <summary>Raised when a sync pass completes for any context.</summary>
    event EventHandler<SyncCompleteEventArgs>? SyncComplete;

    /// <summary>Raised when a sync error occurs in any context.</summary>
    event EventHandler<SyncErrorEventArgs>? SyncError;

    /// <summary>Raised when a sync conflict is detected in any context.</summary>
    event EventHandler<SyncConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>Raised when a sync conflict is auto-resolved in any context.</summary>
    event EventHandler<SyncConflictAutoResolvedEventArgs>? ConflictAutoResolved;

    /// <summary>Raised when per-file transfer progress is reported in any context.</summary>
    event EventHandler<ContextTransferProgressEventArgs>? TransferProgress;

    /// <summary>Raised when an individual file transfer completes in any context.</summary>
    event EventHandler<ContextTransferCompleteEventArgs>? TransferComplete;
}
