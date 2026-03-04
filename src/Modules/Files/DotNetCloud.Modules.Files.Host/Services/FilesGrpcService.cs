using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Host.Protos;
using DotNetCloud.Modules.Files.Models;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Host.Services;

/// <summary>
/// gRPC service implementation for the Files module.
/// Exposes file system operations (CRUD, tree, sharing, trash, versioning) over gRPC
/// for the core server to invoke.
/// </summary>
public sealed class FilesGrpcService : FilesService.FilesServiceBase
{
    private readonly FilesDbContext _db;
    private readonly ILogger<FilesGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesGrpcService"/> class.
    /// </summary>
    public FilesGrpcService(FilesDbContext db, ILogger<FilesGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<CreateFolderResponse> CreateFolder(
        CreateFolderRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new CreateFolderResponse { Success = false, ErrorMessage = "Folder name is required." };
        }

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new CreateFolderResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        Guid? parentId = string.IsNullOrEmpty(request.ParentId)
            ? null
            : Guid.TryParse(request.ParentId, out var pid) ? pid : null;

        // Validate parent exists if specified
        string materializedPath;
        int depth;

        if (parentId.HasValue)
        {
            var parent = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == parentId.Value && n.NodeType == FileNodeType.Folder,
                    context.CancellationToken);

            if (parent is null)
            {
                return new CreateFolderResponse { Success = false, ErrorMessage = "Parent folder not found." };
            }

            materializedPath = $"{parent.MaterializedPath}/{parent.Id}";
            depth = parent.Depth + 1;
        }
        else
        {
            materializedPath = string.Empty;
            depth = 0;
        }

        var folder = new FileNode
        {
            Name = request.Name,
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            ParentId = parentId,
            MaterializedPath = materializedPath,
            Depth = depth
        };
        // Set the full path including this node's ID
        folder.MaterializedPath = string.IsNullOrEmpty(materializedPath)
            ? $"/{folder.Id}"
            : $"{materializedPath}/{folder.Id}";

        _db.FileNodes.Add(folder);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Folder {FolderId} '{Name}' created by user {UserId}", folder.Id, folder.Name, userId);

        return new CreateFolderResponse { Success = true, Node = ToMessage(folder) };
    }

    /// <inheritdoc />
    public override async Task<ListNodesResponse> ListNodes(
        ListNodesRequest request, ServerCallContext context)
    {
        var response = new ListNodesResponse();

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return response;
        }

        Guid? parentId = string.IsNullOrEmpty(request.ParentId)
            ? null
            : Guid.TryParse(request.ParentId, out var pid) ? pid : null;

        var query = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.ParentId == parentId);

        // Sorting
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "size" => request.SortDesc ? query.OrderByDescending(n => n.Size) : query.OrderBy(n => n.Size),
            "updated_at" => request.SortDesc ? query.OrderByDescending(n => n.UpdatedAt) : query.OrderBy(n => n.UpdatedAt),
            "created_at" => request.SortDesc ? query.OrderByDescending(n => n.CreatedAt) : query.OrderBy(n => n.CreatedAt),
            _ => request.SortDesc
                ? query.OrderByDescending(n => n.NodeType).ThenByDescending(n => n.Name)
                : query.OrderBy(n => n.NodeType).ThenBy(n => n.Name)
        };

        response.TotalCount = await query.CountAsync(context.CancellationToken);

        // Pagination
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 500) : 50;
        var page = request.Page > 0 ? request.Page : 1;

        var nodes = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        response.Nodes.AddRange(nodes.Select(ToMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<GetNodeResponse> GetNode(
        GetNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new GetNodeResponse { Found = false };
        }

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, context.CancellationToken);

        if (node is null)
        {
            return new GetNodeResponse { Found = false };
        }

        return new GetNodeResponse { Found = true, Node = ToMessage(node) };
    }

    /// <inheritdoc />
    public override async Task<RenameNodeResponse> RenameNode(
        RenameNodeRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return new RenameNodeResponse { Success = false, ErrorMessage = "New name is required." };
        }

        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new RenameNodeResponse { Success = false, ErrorMessage = "Invalid node ID format." };
        }

        var node = await _db.FileNodes.FindAsync([nodeId], context.CancellationToken);
        if (node is null)
        {
            return new RenameNodeResponse { Success = false, ErrorMessage = "Node not found." };
        }

        node.Name = request.NewName;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {NodeId} renamed to '{NewName}'", nodeId, request.NewName);

        return new RenameNodeResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<MoveNodeResponse> MoveNode(
        MoveNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new MoveNodeResponse { Success = false, ErrorMessage = "Invalid node ID." };
        }

        if (!Guid.TryParse(request.TargetParentId, out var targetParentId))
        {
            return new MoveNodeResponse { Success = false, ErrorMessage = "Invalid target parent ID." };
        }

        var node = await _db.FileNodes.FindAsync([nodeId], context.CancellationToken);
        if (node is null)
        {
            return new MoveNodeResponse { Success = false, ErrorMessage = "Node not found." };
        }

        var targetParent = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == targetParentId && n.NodeType == FileNodeType.Folder,
                context.CancellationToken);

        if (targetParent is null)
        {
            return new MoveNodeResponse { Success = false, ErrorMessage = "Target folder not found." };
        }

        // Prevent moving a folder into itself or its descendants
        if (node.NodeType == FileNodeType.Folder &&
            targetParent.MaterializedPath.StartsWith(node.MaterializedPath, StringComparison.Ordinal))
        {
            return new MoveNodeResponse { Success = false, ErrorMessage = "Cannot move a folder into itself or a descendant." };
        }

        node.ParentId = targetParentId;
        node.MaterializedPath = $"{targetParent.MaterializedPath}/{node.Id}";
        node.Depth = targetParent.Depth + 1;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {NodeId} moved to parent {TargetParentId}", nodeId, targetParentId);

        return new MoveNodeResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<CopyNodeResponse> CopyNode(
        CopyNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId) ||
            !Guid.TryParse(request.TargetParentId, out var targetParentId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new CopyNodeResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var source = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, context.CancellationToken);

        if (source is null)
        {
            return new CopyNodeResponse { Success = false, ErrorMessage = "Source node not found." };
        }

        var targetParent = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == targetParentId && n.NodeType == FileNodeType.Folder,
                context.CancellationToken);

        if (targetParent is null)
        {
            return new CopyNodeResponse { Success = false, ErrorMessage = "Target folder not found." };
        }

        var copy = new FileNode
        {
            Name = source.Name,
            NodeType = source.NodeType,
            MimeType = source.MimeType,
            Size = source.Size,
            ParentId = targetParentId,
            OwnerId = userId,
            ContentHash = source.ContentHash,
            StoragePath = source.StoragePath,
            CurrentVersion = 1,
            Depth = targetParent.Depth + 1
        };
        copy.MaterializedPath = $"{targetParent.MaterializedPath}/{copy.Id}";

        _db.FileNodes.Add(copy);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {SourceId} copied to {CopyId} in parent {TargetParentId}",
            nodeId, copy.Id, targetParentId);

        return new CopyNodeResponse { Success = true, CopiedNode = ToMessage(copy) };
    }

    /// <inheritdoc />
    public override async Task<DeleteNodeResponse> DeleteNode(
        DeleteNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new DeleteNodeResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && !n.IsDeleted, context.CancellationToken);

        if (node is null)
        {
            return new DeleteNodeResponse { Success = false, ErrorMessage = "Node not found." };
        }

        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        node.DeletedByUserId = userId;
        node.OriginalParentId = node.ParentId;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {NodeId} moved to trash by user {UserId}", nodeId, userId);

        return new DeleteNodeResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<ListTrashResponse> ListTrash(
        ListTrashRequest request, ServerCallContext context)
    {
        var response = new ListTrashResponse();

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return response;
        }

        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && n.IsDeleted)
            .OrderByDescending(n => n.DeletedAt)
            .ToListAsync(context.CancellationToken);

        response.Nodes.AddRange(trashedNodes.Select(ToMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<RestoreNodeResponse> RestoreNode(
        RestoreNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new RestoreNodeResponse { Success = false, ErrorMessage = "Invalid node ID." };
        }

        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.IsDeleted, context.CancellationToken);

        if (node is null)
        {
            return new RestoreNodeResponse { Success = false, ErrorMessage = "Trashed node not found." };
        }

        node.IsDeleted = false;
        node.DeletedAt = null;
        node.DeletedByUserId = null;
        node.ParentId = node.OriginalParentId;
        node.OriginalParentId = null;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {NodeId} restored from trash", nodeId);

        return new RestoreNodeResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<PurgeNodeResponse> PurgeNode(
        PurgeNodeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new PurgeNodeResponse { Success = false, ErrorMessage = "Invalid node ID." };
        }

        var node = await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.IsDeleted, context.CancellationToken);

        if (node is null)
        {
            return new PurgeNodeResponse { Success = false, ErrorMessage = "Trashed node not found." };
        }

        _db.FileNodes.Remove(node);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Node {NodeId} permanently deleted", nodeId);

        return new PurgeNodeResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<EmptyTrashResponse> EmptyTrash(
        EmptyTrashRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new EmptyTrashResponse { Success = false };
        }

        var trashedNodes = await _db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.OwnerId == userId && n.IsDeleted)
            .ToListAsync(context.CancellationToken);

        _db.FileNodes.RemoveRange(trashedNodes);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Trash emptied for user {UserId}: {Count} items purged", userId, trashedNodes.Count);

        return new EmptyTrashResponse { Success = true, PurgedCount = trashedNodes.Count };
    }

    /// <inheritdoc />
    public override async Task<InitiateUploadResponse> InitiateUpload(
        InitiateUploadRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return new InitiateUploadResponse { Success = false, ErrorMessage = "File name is required." };
        }

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new InitiateUploadResponse { Success = false, ErrorMessage = "Invalid user ID." };
        }

        // Check which chunks already exist (deduplication)
        var existingHashes = await _db.FileChunks
            .AsNoTracking()
            .Where(c => request.ChunkHashes.Contains(c.ChunkHash))
            .Select(c => c.ChunkHash)
            .ToListAsync(context.CancellationToken);

        var missingHashes = request.ChunkHashes
            .Except(existingHashes)
            .ToList();

        Guid? parentId = string.IsNullOrEmpty(request.ParentId)
            ? null
            : Guid.TryParse(request.ParentId, out var pid) ? pid : (Guid?)null;

        var session = new ChunkedUploadSession
        {
            FileName = request.FileName,
            TargetParentId = parentId,
            TotalSize = request.TotalSize,
            MimeType = request.MimeType,
            TotalChunks = request.ChunkHashes.Count,
            ReceivedChunks = existingHashes.Count,
            ChunkManifest = System.Text.Json.JsonSerializer.Serialize(request.ChunkHashes.ToList()),
            UserId = userId
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Upload session {SessionId} initiated for '{FileName}' ({TotalChunks} chunks, {Existing} existing)",
            session.Id, request.FileName, request.ChunkHashes.Count, existingHashes.Count);

        var response = new InitiateUploadResponse
        {
            Success = true,
            SessionId = session.Id.ToString()
        };
        response.ExistingChunks.AddRange(existingHashes);
        response.MissingChunks.AddRange(missingHashes);

        return response;
    }

    /// <inheritdoc />
    public override async Task<UploadChunkResponse> UploadChunk(
        UploadChunkRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ChunkHash) || request.ChunkData.IsEmpty)
        {
            return new UploadChunkResponse { Success = false, ErrorMessage = "Chunk hash and data are required." };
        }

        // Check if chunk already exists (deduplication)
        var existingChunk = await _db.FileChunks
            .FirstOrDefaultAsync(c => c.ChunkHash == request.ChunkHash, context.CancellationToken);

        if (existingChunk is not null)
        {
            existingChunk.ReferenceCount++;
            existingChunk.LastReferencedAt = DateTime.UtcNow;
        }
        else
        {
            // Store chunk to disk (content-addressable path)
            var storagePath = $"chunks/{request.ChunkHash[..2]}/{request.ChunkHash[2..4]}/{request.ChunkHash}";

            var chunk = new FileChunk
            {
                ChunkHash = request.ChunkHash,
                Size = request.ChunkData.Length,
                StoragePath = storagePath
            };
            _db.FileChunks.Add(chunk);
        }

        // Update session progress if session ID is provided
        if (Guid.TryParse(request.SessionId, out var sessionId))
        {
            var session = await _db.UploadSessions.FindAsync([sessionId], context.CancellationToken);
            if (session is not null)
            {
                session.ReceivedChunks++;
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        return new UploadChunkResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<CompleteUploadResponse> CompleteUpload(
        CompleteUploadRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new CompleteUploadResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var session = await _db.UploadSessions.FindAsync([sessionId], context.CancellationToken);
        if (session is null)
        {
            return new CompleteUploadResponse { Success = false, ErrorMessage = "Upload session not found." };
        }

        // Compute overall content hash from chunk manifest
        var chunkHashes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(session.ChunkManifest) ?? [];
        var combinedHash = ComputeManifestHash(chunkHashes);

        // Build the file storage path
        var storagePath = $"files/{combinedHash[..2]}/{combinedHash[2..4]}/{combinedHash}";

        // Determine parent path
        string materializedPath;
        int depth;

        if (session.TargetParentId.HasValue)
        {
            var parent = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == session.TargetParentId.Value, context.CancellationToken);

            materializedPath = parent is not null ? parent.MaterializedPath : string.Empty;
            depth = parent is not null ? parent.Depth + 1 : 0;
        }
        else
        {
            materializedPath = string.Empty;
            depth = 0;
        }

        // Create the file node
        var fileNode = new FileNode
        {
            Name = session.FileName,
            NodeType = FileNodeType.File,
            MimeType = session.MimeType,
            Size = session.TotalSize,
            ParentId = session.TargetParentId,
            OwnerId = userId,
            ContentHash = combinedHash,
            StoragePath = storagePath,
            CurrentVersion = 1,
            Depth = depth
        };
        fileNode.MaterializedPath = string.IsNullOrEmpty(materializedPath)
            ? $"/{fileNode.Id}"
            : $"{materializedPath}/{fileNode.Id}";

        _db.FileNodes.Add(fileNode);

        // Create version 1
        var version = new FileVersion
        {
            FileNodeId = fileNode.Id,
            VersionNumber = 1,
            Size = session.TotalSize,
            ContentHash = combinedHash,
            StoragePath = storagePath,
            MimeType = session.MimeType,
            CreatedByUserId = userId
        };
        _db.FileVersions.Add(version);

        // Link chunks to the version
        for (var i = 0; i < chunkHashes.Count; i++)
        {
            var chunk = await _db.FileChunks
                .FirstOrDefaultAsync(c => c.ChunkHash == chunkHashes[i], context.CancellationToken);

            if (chunk is not null)
            {
                _db.FileVersionChunks.Add(new FileVersionChunk
                {
                    FileVersionId = version.Id,
                    FileChunkId = chunk.Id,
                    SequenceIndex = i
                });
            }
        }

        // Mark session complete
        session.Status = UploadSessionStatus.Completed;
        session.UpdatedAt = DateTime.UtcNow;

        // Update user quota
        var quota = await _db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, context.CancellationToken);

        if (quota is not null)
        {
            quota.UsedBytes += session.TotalSize;
            quota.LastCalculatedAt = DateTime.UtcNow;
            quota.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Upload completed: {FileName} ({Size} bytes) -> node {NodeId}",
            session.FileName, session.TotalSize, fileNode.Id);

        return new CompleteUploadResponse { Success = true, Node = ToMessage(fileNode) };
    }

    /// <inheritdoc />
    public override async Task DownloadFile(
        DownloadFileRequest request,
        IServerStreamWriter<DownloadFileResponse> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return;
        }

        var versionNumber = request.VersionNumber > 0 ? request.VersionNumber : 0;

        // Find the file version
        FileVersion? version;
        if (versionNumber > 0)
        {
            version = await _db.FileVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FileNodeId == nodeId && v.VersionNumber == versionNumber,
                    context.CancellationToken);
        }
        else
        {
            version = await _db.FileVersions
                .AsNoTracking()
                .Where(v => v.FileNodeId == nodeId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(context.CancellationToken);
        }

        if (version is null)
        {
            return;
        }

        // Get chunks in sequence order
        var chunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Where(vc => vc.FileVersionId == version.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .Include(vc => vc.FileChunk)
            .ToListAsync(context.CancellationToken);

        // Stream chunk data
        foreach (var vc in chunks)
        {
            if (vc.FileChunk is null) continue;

            // In a real implementation, read chunk bytes from disk using vc.FileChunk.StoragePath.
            // For now, send empty chunk as placeholder.
            await responseStream.WriteAsync(new DownloadFileResponse
            {
                ChunkData = Google.Protobuf.ByteString.Empty
            }, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<ListVersionsResponse> ListVersions(
        ListVersionsRequest request, ServerCallContext context)
    {
        var response = new ListVersionsResponse();

        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return response;
        }

        var versions = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == nodeId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(context.CancellationToken);

        response.Versions.AddRange(versions.Select(ToVersionMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<RestoreVersionResponse> RestoreVersion(
        RestoreVersionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new RestoreVersionResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var version = await _db.FileVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.FileNodeId == nodeId && v.VersionNumber == request.VersionNumber,
                context.CancellationToken);

        if (version is null)
        {
            return new RestoreVersionResponse { Success = false, ErrorMessage = "Version not found." };
        }

        var node = await _db.FileNodes.FindAsync([nodeId], context.CancellationToken);
        if (node is null)
        {
            return new RestoreVersionResponse { Success = false, ErrorMessage = "File not found." };
        }

        // Create a new version from the old one
        var maxVersion = await _db.FileVersions
            .Where(v => v.FileNodeId == nodeId)
            .MaxAsync(v => v.VersionNumber, context.CancellationToken);

        var restoredVersion = new FileVersion
        {
            FileNodeId = nodeId,
            VersionNumber = maxVersion + 1,
            Size = version.Size,
            ContentHash = version.ContentHash,
            StoragePath = version.StoragePath,
            MimeType = version.MimeType,
            CreatedByUserId = userId,
            Label = $"Restored from v{version.VersionNumber}"
        };
        _db.FileVersions.Add(restoredVersion);

        // Update node to point to restored content
        node.ContentHash = version.ContentHash;
        node.StoragePath = version.StoragePath;
        node.Size = version.Size;
        node.CurrentVersion = restoredVersion.VersionNumber;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("File {NodeId} restored to version {Version}", nodeId, request.VersionNumber);

        return new RestoreVersionResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<CreateShareResponse> CreateShare(
        CreateShareRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new CreateShareResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        if (!Enum.TryParse<ShareType>(request.ShareType, true, out var shareType))
        {
            return new CreateShareResponse { Success = false, ErrorMessage = "Invalid share type." };
        }

        if (!Enum.TryParse<SharePermission>(request.Permission, true, out var permission))
        {
            permission = SharePermission.Read;
        }

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, context.CancellationToken);

        if (node is null)
        {
            return new CreateShareResponse { Success = false, ErrorMessage = "Node not found." };
        }

        var share = new FileShare
        {
            FileNodeId = nodeId,
            ShareType = shareType,
            Permission = permission,
            CreatedByUserId = userId,
            Note = request.Note
        };

        // Parse target IDs based on share type
        if (shareType == ShareType.User && Guid.TryParse(request.SharedWithId, out var targetUserId))
        {
            share.SharedWithUserId = targetUserId;
        }
        else if (shareType == ShareType.Team && Guid.TryParse(request.SharedWithId, out var targetTeamId))
        {
            share.SharedWithTeamId = targetTeamId;
        }
        else if (shareType == ShareType.Group && Guid.TryParse(request.SharedWithId, out var targetGroupId))
        {
            share.SharedWithGroupId = targetGroupId;
        }
        else if (shareType == ShareType.PublicLink)
        {
            share.LinkToken = GenerateLinkToken();

            if (!string.IsNullOrWhiteSpace(request.LinkPassword))
            {
                share.LinkPasswordHash = HashPassword(request.LinkPassword);
            }

            if (request.MaxDownloads > 0)
            {
                share.MaxDownloads = request.MaxDownloads;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ExpiresAt) &&
            DateTime.TryParse(request.ExpiresAt, out var expiresAt))
        {
            share.ExpiresAt = expiresAt.ToUniversalTime();
        }

        _db.FileShares.Add(share);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Share {ShareId} created for node {NodeId} by user {UserId}", share.Id, nodeId, userId);

        return new CreateShareResponse { Success = true, Share = ToShareMessage(share) };
    }

    /// <inheritdoc />
    public override async Task<ListSharesResponse> ListShares(
        ListSharesRequest request, ServerCallContext context)
    {
        var response = new ListSharesResponse();

        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return response;
        }

        var shares = await _db.FileShares
            .AsNoTracking()
            .Where(s => s.FileNodeId == nodeId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(context.CancellationToken);

        response.Shares.AddRange(shares.Select(ToShareMessage));
        return response;
    }

    /// <inheritdoc />
    public override async Task<RevokeShareResponse> RevokeShare(
        RevokeShareRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ShareId, out var shareId))
        {
            return new RevokeShareResponse { Success = false, ErrorMessage = "Invalid share ID." };
        }

        var share = await _db.FileShares.FindAsync([shareId], context.CancellationToken);
        if (share is null)
        {
            return new RevokeShareResponse { Success = false, ErrorMessage = "Share not found." };
        }

        _db.FileShares.Remove(share);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Share {ShareId} revoked", shareId);

        return new RevokeShareResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<GetQuotaResponse> GetQuota(
        GetQuotaRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new GetQuotaResponse();
        }

        var quota = await _db.FileQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.UserId == userId, context.CancellationToken);

        if (quota is null)
        {
            // Return default unlimited quota
            return new GetQuotaResponse
            {
                MaxBytes = 0,
                UsedBytes = 0,
                RemainingBytes = long.MaxValue,
                UsagePercent = 0.0
            };
        }

        return new GetQuotaResponse
        {
            MaxBytes = quota.MaxBytes,
            UsedBytes = quota.UsedBytes,
            RemainingBytes = quota.RemainingBytes,
            UsagePercent = quota.UsagePercent
        };
    }

    /// <inheritdoc />
    public override async Task<ToggleFavoriteResponse> ToggleFavorite(
        ToggleFavoriteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NodeId, out var nodeId))
        {
            return new ToggleFavoriteResponse { Success = false };
        }

        var node = await _db.FileNodes.FindAsync([nodeId], context.CancellationToken);
        if (node is null)
        {
            return new ToggleFavoriteResponse { Success = false };
        }

        node.IsFavorite = !node.IsFavorite;
        node.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        return new ToggleFavoriteResponse { Success = true, IsFavorite = node.IsFavorite };
    }

    // --- Helper methods ---

    private static FileNodeMessage ToMessage(FileNode node)
    {
        return new FileNodeMessage
        {
            Id = node.Id.ToString(),
            Name = node.Name,
            NodeType = node.NodeType.ToString(),
            MimeType = node.MimeType ?? string.Empty,
            Size = node.Size,
            ParentId = node.ParentId?.ToString() ?? string.Empty,
            OwnerId = node.OwnerId.ToString(),
            CurrentVersion = node.CurrentVersion,
            IsFavorite = node.IsFavorite,
            ContentHash = node.ContentHash ?? string.Empty,
            CreatedAt = node.CreatedAt.ToString("O"),
            UpdatedAt = node.UpdatedAt.ToString("O")
        };
    }

    private static FileVersionMessage ToVersionMessage(FileVersion version)
    {
        return new FileVersionMessage
        {
            Id = version.Id.ToString(),
            VersionNumber = version.VersionNumber,
            Size = version.Size,
            ContentHash = version.ContentHash,
            MimeType = version.MimeType ?? string.Empty,
            CreatedByUserId = version.CreatedByUserId.ToString(),
            CreatedAt = version.CreatedAt.ToString("O"),
            Label = version.Label ?? string.Empty
        };
    }

    private static FileShareMessage ToShareMessage(FileShare share)
    {
        var sharedWithId = share.SharedWithUserId?.ToString()
            ?? share.SharedWithTeamId?.ToString()
            ?? share.SharedWithGroupId?.ToString()
            ?? string.Empty;

        return new FileShareMessage
        {
            Id = share.Id.ToString(),
            NodeId = share.FileNodeId.ToString(),
            ShareType = share.ShareType.ToString(),
            SharedWithId = sharedWithId,
            Permission = share.Permission.ToString(),
            LinkToken = share.LinkToken ?? string.Empty,
            ExpiresAt = share.ExpiresAt?.ToString("O") ?? string.Empty,
            DownloadCount = share.DownloadCount,
            MaxDownloads = share.MaxDownloads ?? 0,
            CreatedAt = share.CreatedAt.ToString("O"),
            Note = share.Note ?? string.Empty
        };
    }

    private static string GenerateLinkToken()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashPassword(string password)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToHexStringLower(hash);
    }

    private static string ComputeManifestHash(List<string> chunkHashes)
    {
        var combined = string.Join(":", chunkHashes);
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexStringLower(hash);
    }
}
