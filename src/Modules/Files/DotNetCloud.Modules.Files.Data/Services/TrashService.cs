using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages trash operations: listing, restoring, and permanently deleting soft-deleted items.
/// </summary>
internal sealed class TrashService : ITrashService
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IEventBus _eventBus;
    private readonly ISyncChangeNotifier _syncNotifier;
    private readonly ILogger<TrashService> _logger;

    public TrashService(FilesDbContext db, IFileStorageEngine storageEngine, IEventBus eventBus, ISyncChangeNotifier syncNotifier, ILogger<TrashService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _eventBus = eventBus;
        _syncNotifier = syncNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrashItemDto>> ListTrashAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId)
            .OrderByDescending(n => n.DeletedAt)
            .ToListAsync(cancellationToken);

        // Report the folder each item will be restored into as a name-based path (not the internal
        // ID-based materialized path) so the trash UI can show a location a user can act on.
        var originalPaths = await BuildOriginalDirectoryPathsAsync(trashedNodes, cancellationToken);

        return trashedNodes
            .Select(n => new TrashItemDto
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                Size = n.Size,
                MimeType = n.MimeType,
                DeletedAt = n.DeletedAt,
                DeletedByUserId = n.DeletedByUserId,
                OriginalPath = originalPaths[n.Id]
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> RestoreAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        EnsureOwnerOrSystem(node, caller);

        // Restoring an item whose parent folder is still in the trash restores the whole deleted
        // ancestor chain, so the subtree lands back on its original path instead of being
        // flattened into the root one node at a time.
        var (restoreRoot, targetParentId) = await ResolveRestoreTargetAsync(node, cancellationToken);

        var now = DateTime.UtcNow;
        await RestoreSubtreeAsync(restoreRoot, targetParentId, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileRestoredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = now,
            FileNodeId = node.Id,
            FileName = node.Name,
            RestoredToParentId = node.ParentId,
            RestoredByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation(
            "Node {NodeId} restored from trash by {UserId} into {ParentId} (subtree root {RestoreRootId})",
            nodeId, caller.UserId, node.ParentId, restoreRoot.Id);

        return ToDto(node);
    }

    /// <inheritdoc />
    public async Task RestoreAllAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId)
            .OrderBy(n => n.DeletedAt)
            .Select(n => new { n.Id, n.OriginalParentId })
            .ToListAsync(cancellationToken);

        var trashedIds = trashedNodes.Select(n => n.Id).ToHashSet();

        // Only the top-level trashed items are restored, and their descendants follow the restored
        // parent. Enrolling descendants here would restore each one individually and flatten the whole
        // tree into the root directory. A top-level item is one whose original parent is not itself in
        // the trash — either it was deleted from the root level, or its original parent was permanently
        // purged (in which case RestoreAsync falls back to the root level and logs it).
        var restoreRootIds = trashedNodes
            .Where(n => n.OriginalParentId is null || !trashedIds.Contains(n.OriginalParentId.Value))
            .Select(n => n.Id)
            .ToList();

        foreach (var restoreRootId in restoreRootIds)
        {
            await RestoreAsync(restoreRootId, caller, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task PermanentDeleteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        EnsureOwnerOrSystem(node, caller);

        // Collect all nodes to delete (self + descendants)
        var nodesToDelete = new List<FileNode> { node };

        if (node.NodeType == FileNodeType.Folder)
        {
            var descendants = await _db.FileNodes
                .IgnoreQueryFilters()
                .Where(n => n.MaterializedPath.StartsWith(node.MaterializedPath + "/"))
                .ToListAsync(cancellationToken);
            nodesToDelete.AddRange(descendants);
        }

        var totalDeletedSize = nodesToDelete
            .Where(n => n.NodeType == FileNodeType.File)
            .Sum(n => n.Size);

        foreach (var n in nodesToDelete)
        {
            await PermanentDeleteNodeAsync(n, cancellationToken);
        }

        await DecrementQuotaAsync(node.OwnerId, totalDeletedSize, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = nodeId,
            FileName = node.Name,
            DeletedByUserId = caller.UserId,
            IsPermanent = true
        }, caller, cancellationToken);

        _logger.LogInformation("Node {NodeId} permanently deleted by {UserId}", nodeId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task EmptyTrashAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var trashItems = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId)
            .ToListAsync(cancellationToken);

        var totalDeletedSize = trashItems
            .Where(n => n.NodeType == FileNodeType.File)
            .Sum(n => n.Size);

        foreach (var item in trashItems)
        {
            await PermanentDeleteNodeAsync(item, cancellationToken);
        }

        await DecrementQuotaAsync(caller.UserId, totalDeletedSize, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trash emptied for user {UserId}. {Count} items permanently deleted.",
            caller.UserId, trashItems.Count);
    }

    /// <inheritdoc />
    public async Task<long> GetTrashSizeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId)
            .SumAsync(n => n.Size, cancellationToken);
    }

    private async Task PermanentDeleteNodeAsync(FileNode node, CancellationToken cancellationToken)
    {
        // Delete shares
        var shares = await _db.FileShares
            .Where(s => s.FileNodeId == node.Id)
            .ToListAsync(cancellationToken);
        _db.FileShares.RemoveRange(shares);

        // Delete tags
        var tags = await _db.FileTags
            .Where(t => t.FileNodeId == node.Id)
            .ToListAsync(cancellationToken);
        _db.FileTags.RemoveRange(tags);

        // Delete comments
        var comments = await _db.FileComments
            .IgnoreQueryFilters()
            .Where(c => c.FileNodeId == node.Id)
            .ToListAsync(cancellationToken);
        _db.FileComments.RemoveRange(comments);

        // Delete versions and decrement chunk refcounts
        var versions = await _db.FileVersions
            .Where(v => v.FileNodeId == node.Id)
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            var versionChunks = await _db.FileVersionChunks
                .Where(vc => vc.FileVersionId == version.Id)
                .ToListAsync(cancellationToken);

            foreach (var vc in versionChunks)
            {
                await ChunkReferenceHelper.DecrementAsync(_db, vc.FileChunkId, cancellationToken);
            }

            _db.FileVersionChunks.RemoveRange(versionChunks);
        }

        _db.FileVersions.RemoveRange(versions);
        _db.FileNodes.Remove(node);
    }

    /// <summary>
    /// Resolves where a trashed node must be restored to.
    /// Walks <see cref="FileNode.OriginalParentId"/> upwards while the ancestor is itself still in the
    /// trash, so an item deleted as part of a folder cascade restores its whole ancestor chain rather
    /// than silently falling back to the root directory.
    /// </summary>
    /// <returns>
    /// The topmost deleted ancestor (the subtree that has to be restored) and the live parent it must
    /// be attached to (<c>null</c> means the root level).
    /// </returns>
    private async Task<(FileNode RestoreRoot, Guid? TargetParentId)> ResolveRestoreTargetAsync(
        FileNode node, CancellationToken cancellationToken)
    {
        var restoreRoot = node;
        var visited = new HashSet<Guid> { node.Id };

        while (restoreRoot.OriginalParentId is Guid originalParentId)
        {
            if (!visited.Add(originalParentId))
            {
                _logger.LogWarning(
                    "Trash restore for node {NodeId}: the ancestor chain contains a cycle at {ParentId}; restoring to the root level.",
                    node.Id, originalParentId);

                return (restoreRoot, null);
            }

            // The soft-delete query filter would hide a trashed parent, which is exactly the case this
            // method has to detect, so the ancestor chain is always read with the filter disabled.
            var parent = await _db.FileNodes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == originalParentId, cancellationToken);

            if (parent is null)
            {
                // The ancestor was permanently purged — there is no directory structure left to rebuild.
                _logger.LogWarning(
                    "Trash restore for node {NodeId}: original parent {ParentId} no longer exists; restoring to the root level.",
                    node.Id, originalParentId);

                return (restoreRoot, null);
            }

            if (!parent.IsDeleted)
            {
                // First live ancestor: the deepest deleted ancestor belongs back inside it.
                return (restoreRoot, parent.Id);
            }

            restoreRoot = parent;
        }

        // Reached the top of the chain: the subtree was deleted from the root level.
        return (restoreRoot, null);
    }

    /// <summary>
    /// Moves a trashed subtree back into <paramref name="targetParentId"/> (or the root level when
    /// <c>null</c>), clears the trash markers, and recomputes <see cref="FileNode.ParentId"/>,
    /// <see cref="FileNode.MaterializedPath"/> and <see cref="FileNode.Depth"/> for every node in the
    /// subtree so the tree stays internally consistent.
    /// </summary>
    private async Task RestoreSubtreeAsync(
        FileNode restoreRoot, Guid? targetParentId, DateTime now, CancellationToken cancellationToken)
    {
        var oldRootPath = restoreRoot.MaterializedPath;

        // Snapshot the descendants before the root's materialized path changes underneath them.
        var descendants = new List<FileNode>();
        if (restoreRoot.NodeType == FileNodeType.Folder)
        {
            descendants = await _db.FileNodes
                .IgnoreQueryFilters()
                .Where(n => n.IsDeleted && n.MaterializedPath.StartsWith(oldRootPath + "/"))
                .ToListAsync(cancellationToken);
        }

        FileNode? targetParent = null;
        if (targetParentId.HasValue)
        {
            targetParent = await _db.FileNodes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == targetParentId.Value, cancellationToken);

            if (targetParent is null)
            {
                _logger.LogWarning(
                    "Trash restore for node {NodeId}: target parent {ParentId} vanished before the restore completed; restoring to the root level.",
                    restoreRoot.Id, targetParentId.Value);

                targetParentId = null;
            }
        }

        // Resolve name conflicts in the target location (auto-rename if needed).
        restoreRoot.Name = await GetRestoreNameAsync(targetParentId, restoreRoot.OwnerId, restoreRoot.Name, cancellationToken);

        restoreRoot.ParentId = targetParentId;
        restoreRoot.IsDeleted = false;
        restoreRoot.DeletedAt = null;
        restoreRoot.DeletedByUserId = null;
        restoreRoot.OriginalParentId = null;
        restoreRoot.UpdatedAt = now;
        restoreRoot.MaterializedPath = targetParent is null
            ? $"/{restoreRoot.Id}"
            : $"{targetParent.MaterializedPath}/{restoreRoot.Id}";
        restoreRoot.Depth = targetParent is null ? 0 : targetParent.Depth + 1;

        await SyncCursorHelper.AssignNextSequenceAsync(_db, restoreRoot, restoreRoot.OwnerId, _syncNotifier, cancellationToken);

        foreach (var descendant in descendants)
        {
            // The subtree keeps its shape, so each descendant's new path is its old path relative to
            // the restored root, re-anchored below the root's new location.
            var relativePath = descendant.MaterializedPath.Length > oldRootPath.Length
                ? descendant.MaterializedPath[oldRootPath.Length..]
                : $"/{descendant.Id}";

            descendant.MaterializedPath = restoreRoot.MaterializedPath + relativePath;
            descendant.Depth = restoreRoot.Depth + relativePath.Count(c => c == '/');
            descendant.IsDeleted = false;
            descendant.DeletedAt = null;
            descendant.DeletedByUserId = null;
            descendant.OriginalParentId = null;
            descendant.UpdatedAt = now;

            // Descendants need their own cursor entries, otherwise delta-sync clients never learn
            // about the rebuilt paths.
            await SyncCursorHelper.AssignNextSequenceAsync(_db, descendant, descendant.OwnerId, _syncNotifier, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the name-based path of the directory each trashed node will be restored into.
    /// Walks <see cref="FileNode.OriginalParentId"/> up to the root (following
    /// <see cref="FileNode.ParentId"/> through live ancestors) and returns <c>"/"</c> for items that
    /// restore to the root level.
    /// </summary>
    private async Task<Dictionary<Guid, string>> BuildOriginalDirectoryPathsAsync(
        IReadOnlyList<FileNode> trashedNodes, CancellationToken cancellationToken)
    {
        var nodesById = new Dictionary<Guid, FileNode?>();
        foreach (var node in trashedNodes)
        {
            nodesById[node.Id] = node;
        }

        var paths = new Dictionary<Guid, string>(trashedNodes.Count);

        foreach (var node in trashedNodes)
        {
            var segments = new List<string>();
            var visited = new HashSet<Guid>();
            var parentId = node.OriginalParentId;

            while (parentId is Guid currentId && visited.Add(currentId))
            {
                if (!nodesById.TryGetValue(currentId, out var parent))
                {
                    parent = await _db.FileNodes
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == currentId, cancellationToken);

                    nodesById[currentId] = parent;
                }

                if (parent is null)
                {
                    // Ancestor was permanently purged — nothing left to describe.
                    break;
                }

                segments.Add(parent.Name);
                parentId = parent.IsDeleted ? parent.OriginalParentId : parent.ParentId;
            }

            segments.Reverse();
            paths[node.Id] = segments.Count == 0 ? "/" : "/" + string.Join('/', segments);
        }

        return paths;
    }

    private static FileNodeDto ToDto(FileNode node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        NodeType = node.NodeType.ToString(),
        MimeType = node.MimeType,
        Size = node.Size,
        ParentId = node.ParentId,
        OwnerId = node.OwnerId,
        CurrentVersion = node.CurrentVersion,
        IsFavorite = node.IsFavorite,
        ContentHash = node.ContentHash,
        CreatedAt = node.CreatedAt,
        UpdatedAt = node.UpdatedAt
    };

    private async Task<string> GetRestoreNameAsync(Guid? parentId, Guid ownerId, string originalName, CancellationToken cancellationToken)
    {
        var name = originalName;
        var counter = 1;

        while (true)
        {
            bool exists;
            if (parentId.HasValue)
                exists = await _db.FileNodes.AnyAsync(n => n.ParentId == parentId && n.Name == name, cancellationToken);
            else
                exists = await _db.FileNodes.AnyAsync(n => n.OwnerId == ownerId && n.ParentId == null && n.Name == name, cancellationToken);

            if (!exists)
                break;

            var ext = Path.GetExtension(originalName);
            var baseName = Path.GetFileNameWithoutExtension(originalName);
            name = $"{baseName} ({counter}){ext}";
            counter++;
        }

        return name;
    }

    private async Task DecrementQuotaAsync(Guid userId, long bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0)
            return;

        // Detaches any stale copy this scoped context tracked earlier, so the decrement is based
        // on the stored usage rather than a snapshot taken at page load (see QuotaRowHelper).
        var quota = await QuotaRowHelper.GetForUpdateAsync(_db, userId, cancellationToken);
        if (quota is not null)
        {
            quota.UsedBytes = Math.Max(0, quota.UsedBytes - bytes);
            quota.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void EnsureOwnerOrSystem(FileNode node, CallerContext caller)
    {
        if (caller.Type == CallerType.System)
            return;

        if (node.OwnerId != caller.UserId)
            throw new ForbiddenException("You do not have permission to manage this trash item.");
    }
}
