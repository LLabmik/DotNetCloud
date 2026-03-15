using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Provides sync operations for desktop/mobile clients: change detection, tree snapshots, and reconciliation.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Gets all changes (created, updated, deleted) since a given timestamp.
    /// Backward-compat fallback for clients that do not yet support cursors.
    /// </summary>
    Task<IReadOnlyList<SyncChangeDto>> GetChangesSinceAsync(DateTime since, Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated, cursor-aware set of changes since the sequence encoded in <paramref name="cursor"/>.
    /// Returns a <see cref="PagedSyncChangesDto"/> containing the page of changes plus a
    /// <c>NextCursor</c> to use for the next request.
    /// If <paramref name="cursor"/> is <c>null</c>, returns all changes (full sync) ordered by sequence number.
    /// </summary>
    Task<PagedSyncChangesDto> GetChangesSinceCursorAsync(string? cursor, Guid? folderId, int limit, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a full folder tree snapshot with content hashes for sync comparison.</summary>
    Task<SyncTreeNodeDto> GetFolderTreeAsync(Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Reconciles the client's file state against the server and produces sync actions.</summary>
    Task<SyncReconcileResultDto> ReconcileAsync(SyncReconcileRequestDto request, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges that a device has successfully processed changes up to a given sequence.
    /// Creates or updates the server-side <c>SyncDeviceCursor</c>.
    /// </summary>
    Task AcknowledgeCursorAsync(Guid userId, Guid deviceId, long acknowledgedSequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server-side cursor for a specific device, enabling cursor recovery
    /// after reinstallation or local state corruption.
    /// </summary>
    Task<DeviceCursorDto> GetDeviceCursorAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sync status for all registered devices across all users (admin-only).
    /// Includes per-device lag relative to each user's current sequence.
    /// </summary>
    Task<IReadOnlyList<DeviceSyncStatusDto>> GetAllDeviceSyncStatusAsync(CancellationToken cancellationToken = default);
}
