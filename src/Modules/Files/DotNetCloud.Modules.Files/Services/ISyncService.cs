using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Provides sync operations for desktop/mobile clients: change detection, tree snapshots, and reconciliation.
/// </summary>
public interface ISyncService
{
    /// <summary>Gets all changes (created, updated, deleted) since a given timestamp.</summary>
    Task<IReadOnlyList<SyncChangeDto>> GetChangesSinceAsync(DateTime since, Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a full folder tree snapshot with content hashes for sync comparison.</summary>
    Task<SyncTreeNodeDto> GetFolderTreeAsync(Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Reconciles the client's file state against the server and produces sync actions.</summary>
    Task<SyncReconcileResultDto> ReconcileAsync(SyncReconcileRequestDto request, CallerContext caller, CancellationToken cancellationToken = default);
}
