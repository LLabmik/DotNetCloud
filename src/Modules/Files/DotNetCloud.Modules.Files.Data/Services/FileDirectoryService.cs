using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Implements <see cref="IFileDirectory"/> providing read-only file directory access
/// backed by the Files module database and storage engine.
/// </summary>
public sealed class FileDirectoryService : IFileDirectory
{
    private readonly FilesDbContext _db;
    private readonly IDownloadService _downloadService;

    /// <summary>
    /// Initializes a new instance of <see cref="FileDirectoryService"/>.
    /// </summary>
    public FileDirectoryService(FilesDbContext db, IDownloadService downloadService)
    {
        _db = db;
        _downloadService = downloadService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeInfo>> ListChildrenAsync(
        Guid userId, Guid? parentId, CancellationToken cancellationToken = default)
    {
        // Virtual root: return the synthetic DotNetCloudRoot folder
        if (parentId is null)
        {
            return
            [
                new FileNodeInfo
                {
                    Id = VirtualMountedNodeRegistry.DotNetCloudRootId,
                    Name = "My Files",
                    NodeType = "Folder",
                    Size = 0
                }
            ];
        }

        // User clicked into DotNetCloudRoot: return real root-level files
        if (parentId == VirtualMountedNodeRegistry.DotNetCloudRootId)
        {
            return await QueryChildrenAsync(userId, null, cancellationToken);
        }

        // Real folder: return its children
        return await QueryChildrenAsync(userId, parentId, cancellationToken);
    }

    private async Task<List<FileNodeInfo>> QueryChildrenAsync(
        Guid userId, Guid? parentId, CancellationToken cancellationToken)
    {
        var query = _db.FileNodes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId && !n.IsDeleted);

        if (parentId.HasValue)
            query = query.Where(n => n.ParentId == parentId.Value);
        else
            query = query.Where(n => n.ParentId == null);

        return await query
            .OrderBy(n => n.NodeType == FileNodeType.Folder ? 0 : 1)
            .ThenBy(n => n.Name)
            .Select(n => new FileNodeInfo
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType == FileNodeType.Folder ? "Folder"
                    : n.NodeType == FileNodeType.SymbolicLink ? "SymbolicLink"
                    : "File",
                MimeType = n.MimeType,
                Size = n.Size,
                ParentId = n.ParentId
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileNodeInfo?> GetFileInfoAsync(
        Guid userId, Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        // Handle virtual root
        if (fileNodeId == VirtualMountedNodeRegistry.DotNetCloudRootId)
        {
            return new FileNodeInfo
            {
                Id = fileNodeId,
                Name = "My Files",
                NodeType = "Folder",
                Size = 0
            };
        }

        return await _db.FileNodes
            .AsNoTracking()
            .Where(n => n.Id == fileNodeId && n.OwnerId == userId && !n.IsDeleted)
            .Select(n => new FileNodeInfo
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType == FileNodeType.Folder ? "Folder"
                    : n.NodeType == FileNodeType.SymbolicLink ? "SymbolicLink"
                    : "File",
                MimeType = n.MimeType,
                Size = n.Size,
                ParentId = n.ParentId
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(
        Guid userId, Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        // Virtual root and folders are not readable
        if (fileNodeId == VirtualMountedNodeRegistry.DotNetCloudRootId)
            return null;

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileNodeId && n.OwnerId == userId
                && !n.IsDeleted && n.NodeType == FileNodeType.File, cancellationToken);

        if (node is null)
            return null;

        try
        {
            var caller = new CallerContext(userId, Array.Empty<string>(), CallerType.User);
            return await _downloadService.DownloadCurrentAsync(fileNodeId, caller, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
