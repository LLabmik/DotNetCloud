using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using SharePermission = DotNetCloud.Modules.Files.Models.SharePermission;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Core file and folder operations backed by the Files database.
/// </summary>
internal sealed class FileService : IFileService
{
    /// <summary>Maximum folder depth to prevent runaway nesting.</summary>
    private const int MaxDepth = 50;

    private readonly FilesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<FileService> _logger;
    private readonly IPermissionService _permissions;

    private readonly IQuotaService _quotaService;

    public FileService(FilesDbContext db, IEventBus eventBus, ILogger<FileService> logger, IPermissionService permissions, IQuotaService quotaService)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _permissions = permissions;
        _quotaService = quotaService;
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> CreateFolderAsync(CreateFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        string parentPath;
        int parentDepth;

        if (dto.ParentId.HasValue)
        {
            var parent = await _db.FileNodes.FindAsync([dto.ParentId.Value], cancellationToken)
                ?? throw new NotFoundException("FileNode", dto.ParentId.Value);

            if (parent.NodeType != FileNodeType.Folder)
                throw new Core.Errors.ValidationException("ParentId", "Parent must be a folder.");

            await _permissions.RequirePermissionAsync(dto.ParentId.Value, caller, SharePermission.ReadWrite, cancellationToken);
            await ValidateNameUniqueAsync(dto.ParentId.Value, dto.Name, null, cancellationToken);

            parentPath = parent.MaterializedPath;
            parentDepth = parent.Depth;
        }
        else
        {
            await ValidateRootNameUniqueAsync(caller.UserId, dto.Name, null, cancellationToken);
            parentPath = "";
            parentDepth = -1;
        }

        if (parentDepth + 1 >= MaxDepth)
            throw new Core.Errors.ValidationException("Depth", $"Maximum folder depth of {MaxDepth} exceeded.");

        var folder = new FileNode
        {
            Name = dto.Name,
            NodeType = FileNodeType.Folder,
            ParentId = dto.ParentId,
            OwnerId = caller.UserId,
            Depth = parentDepth + 1
        };
        folder.MaterializedPath = string.IsNullOrEmpty(parentPath)
            ? $"/{folder.Id}"
            : $"{parentPath}/{folder.Id}";

        _db.FileNodes.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Folder '{Name}' created by {UserId} under {ParentId}",
            dto.Name, caller.UserId, dto.ParentId);

        return ToDto(folder);
    }

    /// <inheritdoc />
    public async Task<FileNodeDto?> GetNodeAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes
            .AsNoTracking()
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken);

        if (node is null)
            return null;

        // Return null (rather than 403) to avoid leaking node existence.
        var perm = await _permissions.GetEffectivePermissionAsync(nodeId, caller, cancellationToken);
        if (perm is null)
            return null;

        var childCount = node.NodeType == FileNodeType.Folder
            ? await _db.FileNodes.CountAsync(n => n.ParentId == nodeId, cancellationToken)
            : 0;

        return ToDto(node, childCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeDto>> ListChildrenAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        await _permissions.RequirePermissionAsync(folderId, caller, SharePermission.Read, cancellationToken);

        var children = await _db.FileNodes
            .AsNoTracking()
            .Include(n => n.Tags)
            .Where(n => n.ParentId == folderId)
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.Name)
            .ToListAsync(cancellationToken);

        return children.Select(n => ToDto(n)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeDto>> ListRootAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var roots = await _db.FileNodes
            .AsNoTracking()
            .Include(n => n.Tags)
            .Where(n => n.OwnerId == caller.UserId && n.ParentId == null)
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.Name)
            .ToListAsync(cancellationToken);

        return roots.Select(n => ToDto(n)).ToList();
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> RenameAsync(Guid nodeId, RenameNodeDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(dto.Name);

        var node = await _db.FileNodes.FindAsync([nodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.ReadWrite, cancellationToken);

        if (node.ParentId.HasValue)
            await ValidateNameUniqueAsync(node.ParentId.Value, dto.Name, nodeId, cancellationToken);
        else
            await ValidateRootNameUniqueAsync(node.OwnerId, dto.Name, nodeId, cancellationToken);

        node.Name = dto.Name;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Node {NodeId} renamed to '{Name}' by {UserId}", nodeId, dto.Name, caller.UserId);

        return ToDto(node);
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> MoveAsync(Guid nodeId, MoveNodeDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes.FindAsync([nodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.ReadWrite, cancellationToken);

        var targetParent = await _db.FileNodes.FindAsync([dto.TargetParentId], cancellationToken)
            ?? throw new NotFoundException("FileNode", dto.TargetParentId);

        if (targetParent.NodeType != FileNodeType.Folder)
            throw new Core.Errors.ValidationException("TargetParentId", "Target must be a folder.");

        await _permissions.RequirePermissionAsync(dto.TargetParentId, caller, SharePermission.ReadWrite, cancellationToken);

        // Prevent circular move: target cannot be self or a descendant
        if (dto.TargetParentId == nodeId)
            throw new Core.Errors.ValidationException("TargetParentId", "Cannot move a node into itself.");

        if (node.NodeType == FileNodeType.Folder)
        {
            var targetPath = targetParent.MaterializedPath;
            if (targetPath.StartsWith(node.MaterializedPath, StringComparison.Ordinal))
                throw new Core.Errors.ValidationException("TargetParentId", "Cannot move a folder into one of its descendants.");
        }

        if (targetParent.Depth + 1 >= MaxDepth)
            throw new Core.Errors.ValidationException("Depth", $"Maximum folder depth of {MaxDepth} exceeded.");

        await ValidateNameUniqueAsync(dto.TargetParentId, node.Name, nodeId, cancellationToken);

        var previousParentId = node.ParentId;
        var oldPath = node.MaterializedPath;

        node.ParentId = dto.TargetParentId;
        node.Depth = targetParent.Depth + 1;
        var newPath = $"{targetParent.MaterializedPath}/{node.Id}";
        node.MaterializedPath = newPath;
        node.UpdatedAt = DateTime.UtcNow;

        // Batch update descendant paths
        if (node.NodeType == FileNodeType.Folder)
        {
            var descendants = await _db.FileNodes
                .Where(n => n.MaterializedPath.StartsWith(oldPath + "/"))
                .ToListAsync(cancellationToken);

            foreach (var desc in descendants)
            {
                desc.MaterializedPath = newPath + desc.MaterializedPath[oldPath.Length..];
                desc.Depth = desc.MaterializedPath.Count(c => c == '/') - 1;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = nodeId,
            FileName = node.Name,
            PreviousParentId = previousParentId,
            NewParentId = dto.TargetParentId,
            MovedByUserId = caller.UserId
        }, caller, cancellationToken);

        _logger.LogInformation("Node {NodeId} moved to parent {ParentId} by {UserId}",
            nodeId, dto.TargetParentId, caller.UserId);

        return ToDto(node);
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> CopyAsync(Guid nodeId, Guid targetParentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var source = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.Read, cancellationToken);

        var targetParent = await _db.FileNodes.FindAsync([targetParentId], cancellationToken)
            ?? throw new NotFoundException("FileNode", targetParentId);

        if (targetParent.NodeType != FileNodeType.Folder)
            throw new Core.Errors.ValidationException("TargetParentId", "Target must be a folder.");

        await _permissions.RequirePermissionAsync(targetParentId, caller, SharePermission.ReadWrite, cancellationToken);

        // Check quota before copying
        var copySize = await CalculateSubtreeSizeAsync(nodeId, source, cancellationToken);
        if (!await _quotaService.HasSufficientQuotaAsync(caller.UserId, copySize, cancellationToken))
            throw new Core.Errors.ValidationException("Quota", "Insufficient storage quota to copy this item.");

        var copyName = await GetCopyNameAsync(targetParentId, source.Name, cancellationToken);

        var copy = new FileNode
        {
            Name = copyName,
            NodeType = source.NodeType,
            MimeType = source.MimeType,
            Size = source.NodeType == FileNodeType.File ? source.Size : 0,
            ParentId = targetParentId,
            OwnerId = caller.UserId,
            Depth = targetParent.Depth + 1,
            ContentHash = source.NodeType == FileNodeType.File ? source.ContentHash : null,
            StoragePath = source.NodeType == FileNodeType.File ? source.StoragePath : null,
            CurrentVersion = 1
        };
        copy.MaterializedPath = $"{targetParent.MaterializedPath}/{copy.Id}";

        _db.FileNodes.Add(copy);

        // Deep copy children for folders
        if (source.NodeType == FileNodeType.Folder)
        {
            await CopyChildrenAsync(nodeId, copy, caller, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Update quota for the copied content
        if (copySize > 0)
            await _quotaService.AdjustUsedBytesAsync(caller.UserId, copySize, cancellationToken);

        _logger.LogInformation("Node {SourceId} copied to {TargetParentId} as {CopyId} by {UserId}",
            nodeId, targetParentId, copy.Id, caller.UserId);

        return ToDto(copy);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes.FindAsync([nodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        await _permissions.RequirePermissionAsync(nodeId, caller, SharePermission.Full, cancellationToken);

        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        node.DeletedByUserId = caller.UserId;
        node.OriginalParentId = node.ParentId;
        node.ParentId = null;

        // Collect all affected node IDs (for share removal)
        var allNodeIds = new List<Guid> { nodeId };

        // Soft-delete descendants
        if (node.NodeType == FileNodeType.Folder)
        {
            var descendants = await _db.FileNodes
                .Where(n => n.MaterializedPath.StartsWith(node.MaterializedPath + "/"))
                .ToListAsync(cancellationToken);

            foreach (var desc in descendants)
            {
                desc.IsDeleted = true;
                desc.DeletedAt = DateTime.UtcNow;
                desc.DeletedByUserId = caller.UserId;
                allNodeIds.Add(desc.Id);
            }
        }

        // Remove shares for all affected nodes (trashed items should not remain shared)
        var shares = await _db.FileShares
            .Where(s => allNodeIds.Contains(s.FileNodeId))
            .ToListAsync(cancellationToken);
        _db.FileShares.RemoveRange(shares);

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new FileDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = nodeId,
            FileName = node.Name,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);

        _logger.LogInformation("Node {NodeId} soft-deleted by {UserId}", nodeId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<FileNodeDto> ToggleFavoriteAsync(Guid nodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes.FindAsync([nodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", nodeId);

        EnsureOwnerOrSystem(node, caller);

        node.IsFavorite = !node.IsFavorite;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(node);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeDto>> ListFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var favorites = await _db.FileNodes
            .AsNoTracking()
            .Include(n => n.Tags)
            .Where(n => n.OwnerId == caller.UserId && n.IsFavorite)
            .OrderBy(n => n.Name)
            .ToListAsync(cancellationToken);

        return favorites.Select(n => ToDto(n)).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<FileNodeDto>> SearchAsync(string query, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var baseQuery = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.Name.Contains(query));

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(n => n.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FileNodeDto>
        {
            Items = items.Select(n => ToDto(n)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeDto>> ListRecentAsync(int count, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (count < 1) count = 10;
        if (count > 100) count = 100;

        var nodes = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.NodeType == FileNodeType.File)
            .OrderByDescending(n => n.UpdatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return nodes.Select(n => ToDto(n)).ToList();
    }

    private async Task ValidateNameUniqueAsync(Guid parentId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = _db.FileNodes.Where(n => n.ParentId == parentId && n.Name == name);
        if (excludeId.HasValue)
            query = query.Where(n => n.Id != excludeId.Value);

        if (await query.AnyAsync(cancellationToken))
            throw new Core.Errors.ValidationException("Name", $"A node named '{name}' already exists in this folder.");
    }

    private async Task ValidateRootNameUniqueAsync(Guid ownerId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var query = _db.FileNodes.Where(n => n.OwnerId == ownerId && n.ParentId == null && n.Name == name);
        if (excludeId.HasValue)
            query = query.Where(n => n.Id != excludeId.Value);

        if (await query.AnyAsync(cancellationToken))
            throw new Core.Errors.ValidationException("Name", $"A node named '{name}' already exists at the root level.");
    }

    private async Task<string> GetCopyNameAsync(Guid parentId, string originalName, CancellationToken cancellationToken)
    {
        var name = originalName;
        var counter = 1;

        while (await _db.FileNodes.AnyAsync(n => n.ParentId == parentId && n.Name == name, cancellationToken))
        {
            var ext = Path.GetExtension(originalName);
            var baseName = Path.GetFileNameWithoutExtension(originalName);
            name = $"{baseName} ({counter}){ext}";
            counter++;
        }

        return name;
    }

    private async Task CopyChildrenAsync(Guid sourceParentId, FileNode copyParent, CallerContext caller, CancellationToken cancellationToken)
    {
        var children = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.ParentId == sourceParentId)
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var childCopy = new FileNode
            {
                Name = child.Name,
                NodeType = child.NodeType,
                MimeType = child.MimeType,
                Size = child.NodeType == FileNodeType.File ? child.Size : 0,
                ParentId = copyParent.Id,
                OwnerId = caller.UserId,
                Depth = copyParent.Depth + 1,
                ContentHash = child.NodeType == FileNodeType.File ? child.ContentHash : null,
                StoragePath = child.NodeType == FileNodeType.File ? child.StoragePath : null,
                CurrentVersion = 1
            };
            childCopy.MaterializedPath = $"{copyParent.MaterializedPath}/{childCopy.Id}";

            _db.FileNodes.Add(childCopy);

            if (child.NodeType == FileNodeType.Folder)
            {
                await CopyChildrenAsync(child.Id, childCopy, caller, cancellationToken);
            }
        }
    }

    /// <summary>Returns the total file size of a node and all its non-deleted descendants.</summary>
    private async Task<long> CalculateSubtreeSizeAsync(Guid nodeId, FileNode source, CancellationToken cancellationToken)
    {
        if (source.NodeType == FileNodeType.File)
            return source.Size;

        // Sum all non-deleted file nodes whose materialized path begins under this folder
        var subtreeSize = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.MaterializedPath.StartsWith(source.MaterializedPath + "/")
                        && n.NodeType == FileNodeType.File
                        && !n.IsDeleted)
            .SumAsync(n => n.Size, cancellationToken);

        return subtreeSize;
    }

    private static void EnsureOwnerOrSystem(FileNode node, CallerContext caller)
    {
        if (caller.Type == CallerType.System)
            return;

        if (node.OwnerId != caller.UserId)
            throw new ForbiddenException("You do not have permission to modify this node.");
    }

    private static FileNodeDto ToDto(FileNode node, int? childCount = null) => new()
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
        UpdatedAt = node.UpdatedAt,
        ChildCount = childCount ?? 0,
        Tags = node.Tags?.Select(t => t.Name).ToList() ?? []
    };
}
