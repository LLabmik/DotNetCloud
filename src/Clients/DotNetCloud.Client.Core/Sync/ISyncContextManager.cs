using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.SelectiveSync;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Manages active sync contexts — one per OS-user + server-account pair.
/// Orchestrates the lifecycle of per-context <see cref="ISyncEngine"/> instances.
/// </summary>
public interface ISyncContextManager
{
    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads persisted context registrations from the system registry file
    /// and starts a sync engine for each. Called once at startup.
    /// </summary>
    Task LoadContextsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all running sync engines. Called during shutdown.
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
    /// Adds an additional local sync folder to an existing account, reusing its stored tokens.
    /// Each folder becomes its own sync context, optionally scoped to a remote folder.
    /// </summary>
    Task<SyncContextRegistration> AddFolderAsync(
        Guid existingContextId,
        string localFolderPath,
        Guid? serverFolderId,
        string? serverFolderDisplayPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a sync context, stops and disposes its engine, deletes its
    /// stored tokens, and removes it from the persisted registry.
    /// </summary>
    Task RemoveContextAsync(Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-authenticates an existing account without removing it: replaces the stored OAuth2
    /// tokens for every sync context of the account (they share the same account key) and
    /// restarts any offline sync engines so the account comes back online. No contexts,
    /// folder mappings, or selective-sync rules are lost.
    /// </summary>
    /// <param name="contextId">Any context ID belonging to the account to reconnect.</param>
    /// <param name="accessToken">New access token.</param>
    /// <param name="refreshToken">New refresh token.</param>
    /// <param name="expiresAt">UTC expiry time of the new access token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sync contexts that were updated.</returns>
    Task<int> ReauthenticateAccountAsync(
        Guid contextId,
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Marks all unresolved conflicts across all contexts as resolved with the given
    /// resolution string. Returns the total number of conflicts resolved.
    /// </summary>
    Task<int> BatchResolveConflictsAsync(string resolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the global bandwidth throttle limits, persists them to <c>sync-settings.json</c>,
    /// and updates all registrations for newly created engines.
    /// </summary>
    Task UpdateBandwidthAsync(
        decimal uploadLimitKbps, decimal downloadLimitKbps,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies selective sync rules for the given context and persists them to the
    /// context's per-context state database.
    /// </summary>
    Task UpdateSelectiveSyncAsync(
        Guid contextId,
        IReadOnlyList<SelectiveSyncRule> rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current selective sync rules for the given context (loaded from the
    /// per-context state database).
    /// </summary>
    Task<IReadOnlyList<SelectiveSyncRule>> GetSelectiveSyncRulesAsync(
        Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a folder size-limit decision for the given context: records a <c>SizeLimit</c>
    /// rule (include or exclude) and persists it, so the engine skips (or syncs) the folder.
    /// </summary>
    Task ApplySizeLimitDecisionAsync(
        Guid contextId, string relativePath, bool syncFolder, CancellationToken cancellationToken = default);

    /// <summary>Applies the folder size limit settings to all running contexts.</summary>
    Task SetSizeLimitSettingsAsync(bool enabled, long maxFolderSizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists conflict resolution settings to <c>sync-settings.json</c> and
    /// applies them to all active engines.
    /// </summary>
    Task PersistConflictResolutionSettingsAsync(
        DotNetCloud.Client.Core.Conflict.ConflictResolutionSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the server-side folder tree for the given context (for selective sync UI).
    /// </summary>
    Task<SyncTreeNodeResponse?> GetFolderTreeAsync(
        Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new remote folder on the server for the given context and returns the created node.
    /// </summary>
    /// <param name="contextId">Sync context ID whose credentials to use.</param>
    /// <param name="name">New folder name (must not contain path separators).</param>
    /// <param name="parentId">Parent folder node ID, or <c>null</c> to create at the account root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileNodeResponse> CreateRemoteFolderAsync(
        Guid contextId,
        string name,
        Guid? parentId,
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

    /// <summary>Raised when a folder exceeds the folder size limit and no decision has been recorded yet.</summary>
    event EventHandler<SizeLimitDecisionRequestedEventArgs>? SizeLimitDecisionRequested;
}
