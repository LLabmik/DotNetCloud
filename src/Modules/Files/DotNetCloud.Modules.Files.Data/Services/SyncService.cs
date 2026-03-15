using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Provides sync operations for desktop/mobile clients.
/// </summary>
internal sealed class SyncService : ISyncService
{
    private const int MaxPageLimit = 5000;
    private const int DefaultPageLimit = 500;

    private readonly FilesDbContext _db;
    private readonly ILogger<SyncService> _logger;
    private readonly IDeviceContext _deviceContext;

    public SyncService(FilesDbContext db, ILogger<SyncService> logger, IDeviceContext deviceContext)
    {
        _db = db;
        _logger = logger;
        _deviceContext = deviceContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncChangeDto>> GetChangesSinceAsync(DateTime since, Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        // Active nodes updated since the given time
        var activeQuery = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.UpdatedAt >= since);

        if (folderId.HasValue)
            activeQuery = activeQuery.Where(n => n.ParentId == folderId.Value || n.Id == folderId.Value);

        var activeChanges = await activeQuery
            .Select(n => new SyncChangeDto
            {
                NodeId = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                ParentId = n.ParentId,
                ContentHash = n.ContentHash,
                Size = n.Size,
                UpdatedAt = n.UpdatedAt,
                IsDeleted = false,
                SyncSequence = n.SyncSequence,
                OriginatingDeviceId = n.OriginatingDeviceId,
                PosixMode = n.PosixMode,
                PosixOwnerHint = n.PosixOwnerHint,
                LinkTarget = n.LinkTarget
            })
            .ToListAsync(cancellationToken);

        // Deleted nodes since the given time
        var deletedQuery = _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.IsDeleted && n.DeletedAt >= since);

        if (folderId.HasValue)
            deletedQuery = deletedQuery.Where(n => n.ParentId == folderId.Value || n.Id == folderId.Value);

        var deletedChanges = await deletedQuery
            .Select(n => new SyncChangeDto
            {
                NodeId = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                ParentId = n.ParentId,
                ContentHash = n.ContentHash,
                Size = n.Size,
                UpdatedAt = n.UpdatedAt,
                IsDeleted = true,
                DeletedAt = n.DeletedAt,
                SyncSequence = n.SyncSequence,
                OriginatingDeviceId = n.OriginatingDeviceId,
                PosixMode = n.PosixMode,
                PosixOwnerHint = n.PosixOwnerHint,
                LinkTarget = n.LinkTarget
            })
            .ToListAsync(cancellationToken);

        var allChanges = activeChanges.Concat(deletedChanges)
            .OrderByDescending(c => c.UpdatedAt)
            .ToList();

        if (_deviceContext.DeviceId.HasValue)
        {
            var currentDeviceId = _deviceContext.DeviceId.Value;
            var beforeCount = allChanges.Count;
            allChanges = allChanges
                .Where(c => c.OriginatingDeviceId != currentDeviceId)
                .ToList();

            var suppressed = beforeCount - allChanges.Count;
            if (suppressed > 0)
            {
                _logger.LogInformation(
                    "sync.changes.suppressed_self_origin {UserId} {DeviceId} {SuppressedCount}",
                    caller.UserId,
                    currentDeviceId,
                    suppressed);
            }
        }

        return allChanges;
    }

    /// <inheritdoc />
    public async Task<PagedSyncChangesDto> GetChangesSinceCursorAsync(string? cursor, Guid? folderId, int limit, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var effectiveLimit = Math.Clamp(limit, 1, MaxPageLimit);

        long sinceSequence = 0;
        if (cursor is not null)
        {
            var decoded = SyncCursorHelper.DecodeCursor(cursor);
            if (decoded is null || decoded.Value.UserId != caller.UserId)
            {
                // Invalid or mismatched cursor — start from beginning
                sinceSequence = 0;
                _logger.LogWarning("sync.cursor.invalid {UserId} cursor='{Cursor}'", caller.UserId, cursor);
            }
            else
            {
                sinceSequence = decoded.Value.Sequence;
            }
        }

        // Active nodes with SyncSequence > sinceSequence
        var activeQuery = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId &&
                        n.SyncSequence != null &&
                        n.SyncSequence > sinceSequence);

        if (folderId.HasValue)
            activeQuery = activeQuery.Where(n => n.ParentId == folderId.Value || n.Id == folderId.Value);

        // Deleted nodes with SyncSequence > sinceSequence
        var deletedQuery = _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId &&
                        n.IsDeleted &&
                        n.SyncSequence != null &&
                        n.SyncSequence > sinceSequence);

        if (folderId.HasValue)
            deletedQuery = deletedQuery.Where(n => n.ParentId == folderId.Value || n.Id == folderId.Value);

        // Fetch one extra item to determine hasMore
        var fetchLimit = effectiveLimit + 1;

        var activeChanges = await activeQuery
            .OrderBy(n => n.SyncSequence)
            .Take(fetchLimit)
            .Select(n => new SyncChangeDto
            {
                NodeId = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                ParentId = n.ParentId,
                ContentHash = n.ContentHash,
                Size = n.Size,
                UpdatedAt = n.UpdatedAt,
                IsDeleted = false,
                SyncSequence = n.SyncSequence,
                OriginatingDeviceId = n.OriginatingDeviceId,
                PosixMode = n.PosixMode,
                PosixOwnerHint = n.PosixOwnerHint,
                LinkTarget = n.LinkTarget
            })
            .ToListAsync(cancellationToken);

        var deletedChanges = await deletedQuery
            .OrderBy(n => n.SyncSequence)
            .Take(fetchLimit)
            .Select(n => new SyncChangeDto
            {
                NodeId = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                ParentId = n.ParentId,
                ContentHash = n.ContentHash,
                Size = n.Size,
                UpdatedAt = n.UpdatedAt,
                IsDeleted = true,
                DeletedAt = n.DeletedAt,
                SyncSequence = n.SyncSequence,
                OriginatingDeviceId = n.OriginatingDeviceId,
                PosixMode = n.PosixMode,
                PosixOwnerHint = n.PosixOwnerHint,
                LinkTarget = n.LinkTarget
            })
            .ToListAsync(cancellationToken);

        // Merge, sort by SyncSequence, pick the window
        var allChanges = activeChanges.Concat(deletedChanges)
            .OrderBy(c => c.SyncSequence)
            .ToList();

        var maxSequenceInWindow = allChanges.Count > 0
            ? allChanges.Max(c => c.SyncSequence ?? sinceSequence)
            : sinceSequence;
        var suppressedSelfOriginCount = 0;

        if (_deviceContext.DeviceId.HasValue)
        {
            var currentDeviceId = _deviceContext.DeviceId.Value;
            var beforeCount = allChanges.Count;
            allChanges = allChanges
                .Where(c => c.OriginatingDeviceId != currentDeviceId)
                .ToList();

            suppressedSelfOriginCount = beforeCount - allChanges.Count;
            if (suppressedSelfOriginCount > 0)
            {
                _logger.LogInformation(
                    "sync.cursor.suppressed_self_origin {UserId} {DeviceId} {SuppressedCount}",
                    caller.UserId,
                    currentDeviceId,
                    suppressedSelfOriginCount);
            }
        }

        var hasMore = allChanges.Count > effectiveLimit;
        var page = allChanges.Take(effectiveLimit).ToList();
        var maxSequenceInPage = page.Count > 0
            ? page.Max(c => c.SyncSequence ?? sinceSequence)
            : sinceSequence;
        var nextSequence = suppressedSelfOriginCount > 0 && maxSequenceInWindow > maxSequenceInPage
            ? maxSequenceInWindow
            : maxSequenceInPage;
        var nextCursor = SyncCursorHelper.EncodeCursor(caller.UserId, nextSequence);

        return new PagedSyncChangesDto
        {
            Changes = page,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }

    /// <inheritdoc />
    public async Task<SyncTreeNodeDto> GetFolderTreeAsync(Guid? folderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (folderId.HasValue)
        {
            var folder = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == folderId.Value && n.OwnerId == caller.UserId, cancellationToken)
                ?? throw new NotFoundException("FileNode", folderId.Value);

            return await BuildTreeNodeAsync(folder, caller.UserId, cancellationToken);
        }

        // Build a virtual root containing all root-level nodes
        var rootNodes = await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.ParentId == null)
            .OrderBy(n => n.NodeType)
            .ThenBy(n => n.Name)
            .ToListAsync(cancellationToken);

        var children = new List<SyncTreeNodeDto>();
        foreach (var node in rootNodes)
        {
            children.Add(await BuildTreeNodeAsync(node, caller.UserId, cancellationToken));
        }

        return new SyncTreeNodeDto
        {
            NodeId = Guid.Empty,
            Name = "/",
            NodeType = "Folder",
            Size = 0,
            UpdatedAt = DateTime.UtcNow,
            Children = children
        };
    }

    /// <inheritdoc />
    public async Task<SyncReconcileResultDto> ReconcileAsync(SyncReconcileRequestDto request, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        // Build server state map
        var serverQuery = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId);

        if (request.FolderId.HasValue)
        {
            var folder = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == request.FolderId.Value, cancellationToken);

            if (folder is not null)
            {
                serverQuery = serverQuery.Where(n =>
                    n.Id == request.FolderId.Value ||
                    n.MaterializedPath.StartsWith(folder.MaterializedPath + "/"));
            }
        }

        var serverNodes = await serverQuery.ToListAsync(cancellationToken);
        var serverMap = serverNodes.ToDictionary(n => n.Id);

        // Also get deleted nodes
        var deletedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.OwnerId == caller.UserId && n.IsDeleted)
            .Select(n => n.Id)
            .ToHashSetAsync(cancellationToken);

        var clientMap = request.ClientNodes.ToDictionary(c => c.NodeId);
        var actions = new List<SyncActionDto>();

        // Server has it, client doesn't → Download
        foreach (var serverNode in serverNodes)
        {
            if (!clientMap.ContainsKey(serverNode.Id))
            {
                actions.Add(new SyncActionDto
                {
                    NodeId = serverNode.Id,
                    Action = "Download",
                    Reason = "New on server"
                });
            }
        }

        foreach (var clientNode in request.ClientNodes)
        {
            if (deletedNodes.Contains(clientNode.NodeId))
            {
                // Server deleted it → client should delete
                actions.Add(new SyncActionDto
                {
                    NodeId = clientNode.NodeId,
                    Action = "Delete",
                    Reason = "Deleted on server"
                });
            }
            else if (serverMap.TryGetValue(clientNode.NodeId, out var serverNode))
            {
                // Both have it — compare hashes
                if (clientNode.ContentHash != serverNode.ContentHash)
                {
                    if (clientNode.UpdatedAt > serverNode.UpdatedAt)
                    {
                        actions.Add(new SyncActionDto
                        {
                            NodeId = clientNode.NodeId,
                            Action = "Upload",
                            Reason = "Client is newer"
                        });
                    }
                    else if (clientNode.UpdatedAt < serverNode.UpdatedAt)
                    {
                        actions.Add(new SyncActionDto
                        {
                            NodeId = clientNode.NodeId,
                            Action = "Download",
                            Reason = "Server is newer"
                        });
                    }
                    else
                    {
                        actions.Add(new SyncActionDto
                        {
                            NodeId = clientNode.NodeId,
                            Action = "Conflict",
                            Reason = "Same timestamp but different content"
                        });
                    }
                }
            }
            else
            {
                // Client has it, server doesn't and it's not deleted → Upload
                actions.Add(new SyncActionDto
                {
                    NodeId = clientNode.NodeId,
                    Action = "Upload",
                    Reason = "New on client"
                });
            }
        }

        _logger.LogInformation("Sync reconcile for {UserId}: {ActionCount} actions produced", caller.UserId, actions.Count);

        return new SyncReconcileResultDto { Actions = actions };
    }

    private async Task<SyncTreeNodeDto> BuildTreeNodeAsync(FileNode node, Guid ownerId, CancellationToken cancellationToken)
    {
        var children = new List<SyncTreeNodeDto>();

        if (node.NodeType == FileNodeType.Folder)
        {
            var childNodes = await _db.FileNodes
                .AsNoTracking()
                .Where(n => n.ParentId == node.Id && n.OwnerId == ownerId)
                .OrderBy(n => n.NodeType)
                .ThenBy(n => n.Name)
                .ToListAsync(cancellationToken);

            foreach (var child in childNodes)
            {
                children.Add(await BuildTreeNodeAsync(child, ownerId, cancellationToken));
            }
        }

        return new SyncTreeNodeDto
        {
            NodeId = node.Id,
            Name = node.Name,
            NodeType = node.NodeType.ToString(),
            ContentHash = node.ContentHash,
            Size = node.Size,
            UpdatedAt = node.UpdatedAt,
            Children = children,
            PosixMode = node.PosixMode,
            PosixOwnerHint = node.PosixOwnerHint,
            LinkTarget = node.LinkTarget
        };
    }

    /// <inheritdoc />
    public async Task AcknowledgeCursorAsync(Guid userId, Guid deviceId, long acknowledgedSequence, CancellationToken cancellationToken = default)
    {
        // Verify the device belongs to this user
        var device = await _db.SyncDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, cancellationToken);

        if (device is null)
            throw new Core.Errors.NotFoundException($"Device {deviceId} not found for user.");

        if (acknowledgedSequence < 0)
            throw new Core.Errors.ValidationException("AcknowledgedSequence", "Sequence must be non-negative.");

        var cursor = await _db.SyncDeviceCursors.FindAsync([deviceId], cancellationToken);
        if (cursor is null)
        {
            cursor = new Models.SyncDeviceCursor
            {
                DeviceId = deviceId,
                UserId = userId,
                LastAcknowledgedSequence = acknowledgedSequence,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.SyncDeviceCursors.Add(cursor);
        }
        else
        {
            // Only advance forward — never regress the cursor
            if (acknowledgedSequence > cursor.LastAcknowledgedSequence)
            {
                cursor.LastAcknowledgedSequence = acknowledgedSequence;
                cursor.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Device {DeviceId} acked sequence {Sequence} for user {UserId}.",
            deviceId, acknowledgedSequence, userId);
    }

    /// <inheritdoc />
    public async Task<DeviceCursorDto> GetDeviceCursorAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        // Verify the device belongs to this user
        var device = await _db.SyncDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, cancellationToken);

        if (device is null)
            throw new Core.Errors.NotFoundException($"Device {deviceId} not found for user.");

        var cursor = await _db.SyncDeviceCursors.AsNoTracking()
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId, cancellationToken);

        if (cursor is null)
        {
            return new DeviceCursorDto
            {
                DeviceId = deviceId,
                LastAcknowledgedSequence = null,
                Cursor = null,
                UpdatedAt = null,
            };
        }

        return new DeviceCursorDto
        {
            DeviceId = deviceId,
            LastAcknowledgedSequence = cursor.LastAcknowledgedSequence,
            Cursor = SyncCursorHelper.EncodeCursor(userId, cursor.LastAcknowledgedSequence),
            UpdatedAt = cursor.UpdatedAt,
        };
    }
}
