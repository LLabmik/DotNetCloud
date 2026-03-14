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
    private readonly ILogger<TrashService> _logger;

    public TrashService(FilesDbContext db, IFileStorageEngine storageEngine, IEventBus eventBus, ILogger<TrashService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrashItemDto>> ListTrashAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId && n.OriginalParentId != null)
            .OrderByDescending(n => n.DeletedAt)
            .Select(n => new TrashItemDto
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                Size = n.Size,
                MimeType = n.MimeType,
                DeletedAt = n.DeletedAt,
                DeletedByUserId = n.DeletedByUserId,
                OriginalPath = n.MaterializedPath
            })
            .ToListAsync(cancellationToken);
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

        // Determine restore target
        Guid? restoreParentId = null;
        if (node.OriginalParentId.HasValue)
        {
            var originalParent = await _db.FileNodes
                .FirstOrDefaultAsync(n => n.Id == node.OriginalParentId.Value, cancellationToken);

            restoreParentId = originalParent?.Id;
        }

        // Resolve name conflicts in the target location (auto-rename if needed)
        node.Name = await GetRestoreNameAsync(restoreParentId, node.OwnerId, node.Name, cancellationToken);

        // If original parent is gone, restore to root
        node.ParentId = restoreParentId;
        node.IsDeleted = false;
        node.DeletedAt = null;
        node.DeletedByUserId = null;
        node.OriginalParentId = null;
        node.UpdatedAt = DateTime.UtcNow;

        // Rebuild materialized path
        if (restoreParentId.HasValue)
        {
            var parent = await _db.FileNodes.FindAsync([restoreParentId.Value], cancellationToken);
            if (parent is not null)
            {
                node.MaterializedPath = $"{parent.MaterializedPath}/{node.Id}";
                node.Depth = parent.Depth + 1;
            }
        }
        else
        {
            node.MaterializedPath = $"/{node.Id}";
            node.Depth = 0;
        }

        // Restore descendants
        if (node.NodeType == FileNodeType.Folder)
        {
            await RestoreDescendantsAsync(node, cancellationToken);
        }

        await SyncCursorHelper.AssignNextSequenceAsync(_db, node, node.OwnerId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileRestoredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = nodeId,
            FileName = node.Name,
            RestoredToParentId = restoreParentId,
            RestoredByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Node {NodeId} restored from trash by {UserId}", nodeId, caller.UserId);

        return new FileNodeDto
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
    }

    /// <inheritdoc />
    public async Task RestoreAllAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        // Get top-level deleted items (those with OriginalParentId set)
        var trashItems = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && n.OwnerId == caller.UserId && n.OriginalParentId != null)
            .ToListAsync(cancellationToken);

        foreach (var item in trashItems)
        {
            await RestoreAsync(item.Id, caller, cancellationToken);
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
            EventId = Guid.NewGuid(),
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

    private async Task RestoreDescendantsAsync(FileNode parentNode, CancellationToken cancellationToken)
    {
        var descendants = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && n.MaterializedPath.StartsWith(parentNode.MaterializedPath + "/"))
            .ToListAsync(cancellationToken);

        foreach (var desc in descendants)
        {
            desc.IsDeleted = false;
            desc.DeletedAt = null;
            desc.DeletedByUserId = null;
        }
    }

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

            if (!exists) break;

            var ext = Path.GetExtension(originalName);
            var baseName = Path.GetFileNameWithoutExtension(originalName);
            name = $"{baseName} ({counter}){ext}";
            counter++;
        }

        return name;
    }

    private async Task DecrementQuotaAsync(Guid userId, long bytes, CancellationToken cancellationToken)
    {
        if (bytes <= 0) return;

        var quota = await _db.FileQuotas.FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);
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
