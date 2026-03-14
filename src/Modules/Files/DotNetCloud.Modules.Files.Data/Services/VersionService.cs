using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharePermission = DotNetCloud.Modules.Files.Models.SharePermission;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages file version history.
/// </summary>
internal sealed class VersionService : IVersionService
{
    private readonly FilesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<VersionService> _logger;
    private readonly IPermissionService _permissions;

    public VersionService(FilesDbContext db, IEventBus eventBus, ILogger<VersionService> logger, IPermissionService permissions)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.Read, cancellationToken);

        return await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == fileNodeId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new FileVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Size = v.Size,
                ContentHash = v.ContentHash,
                MimeType = v.MimeType,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAt = v.CreatedAt,
                Label = v.Label
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileVersionDto?> GetVersionAsync(Guid versionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var version = await _db.FileVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version is not null)
            await _permissions.RequirePermissionAsync(version.FileNodeId, caller, SharePermission.Read, cancellationToken);

        return version is null ? null : ToDto(version);
    }

    /// <inheritdoc />
    public async Task<FileVersionDto> RestoreVersionAsync(Guid fileNodeId, Guid versionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var node = await _db.FileNodes.FindAsync([fileNodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.ReadWrite, cancellationToken);

        if (node.NodeType != FileNodeType.File)
            throw new Core.Errors.InvalidOperationException("Cannot restore versions on a folder.");

        var sourceVersion = await _db.FileVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FileNodeId == fileNodeId, cancellationToken)
            ?? throw new NotFoundException("FileVersion", versionId);

        // Create a new version pointing to the same content
        node.CurrentVersion++;
        node.ContentHash = sourceVersion.ContentHash;
        node.StoragePath = sourceVersion.StoragePath;
        node.Size = sourceVersion.Size;
        node.UpdatedAt = DateTime.UtcNow;

        var newVersion = new FileVersion
        {
            FileNodeId = fileNodeId,
            VersionNumber = node.CurrentVersion,
            Size = sourceVersion.Size,
            ContentHash = sourceVersion.ContentHash,
            StoragePath = sourceVersion.StoragePath,
            MimeType = sourceVersion.MimeType,
            CreatedByUserId = caller.UserId,
            Label = $"Restored from v{sourceVersion.VersionNumber}"
        };
        _db.FileVersions.Add(newVersion);

        // Copy chunk mappings and increment refcounts
        var sourceChunks = await _db.FileVersionChunks
            .Where(vc => vc.FileVersionId == sourceVersion.Id)
            .ToListAsync(cancellationToken);

        foreach (var sc in sourceChunks)
        {
            _db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = newVersion.Id,
                FileChunkId = sc.FileChunkId,
                SequenceIndex = sc.SequenceIndex
            });

            await ChunkReferenceHelper.IncrementAsync(_db, sc.FileChunkId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("File {FileNodeId} restored to version {SourceVersion} as v{NewVersion} by {UserId}",
            fileNodeId, sourceVersion.VersionNumber, newVersion.VersionNumber, caller.UserId);

        await _eventBus.PublishAsync(new FileVersionRestoredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            FileName = node.Name,
            SourceVersionId = sourceVersion.Id,
            SourceVersionNumber = sourceVersion.VersionNumber,
            NewVersionNumber = newVersion.VersionNumber,
            RestoredByUserId = caller.UserId
        }, caller, cancellationToken);

        return ToDto(newVersion);
    }

    /// <inheritdoc />
    public async Task<FileVersionDto> LabelVersionAsync(Guid versionId, string label, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var version = await _db.FileVersions
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
            ?? throw new NotFoundException("FileVersion", versionId);

        await _permissions.RequirePermissionAsync(version.FileNodeId, caller, SharePermission.ReadWrite, cancellationToken);

        version.Label = label;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(version);
    }

    /// <inheritdoc />
    public async Task DeleteVersionAsync(Guid versionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var version = await _db.FileVersions
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
            ?? throw new NotFoundException("FileVersion", versionId);

        await _permissions.RequirePermissionAsync(version.FileNodeId, caller, SharePermission.Full, cancellationToken);

        // Ensure this is not the only version
        var versionCount = await _db.FileVersions
            .CountAsync(v => v.FileNodeId == version.FileNodeId, cancellationToken);

        if (versionCount <= 1)
            throw new Core.Errors.InvalidOperationException("Cannot delete the only remaining version of a file.");

        // Decrement refcounts on chunks
        var versionChunks = await _db.FileVersionChunks
            .Where(vc => vc.FileVersionId == versionId)
            .ToListAsync(cancellationToken);

        foreach (var vc in versionChunks)
        {
            await ChunkReferenceHelper.DecrementAsync(_db, vc.FileChunkId, cancellationToken);
        }

        _db.FileVersionChunks.RemoveRange(versionChunks);
        _db.FileVersions.Remove(version);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Version {VersionId} (v{VersionNumber}) deleted by {UserId}",
            versionId, version.VersionNumber, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<FileVersionDto?> GetVersionByNumberAsync(Guid fileNodeId, int versionNumber, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var version = await _db.FileVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.FileNodeId == fileNodeId && v.VersionNumber == versionNumber, cancellationToken);

        if (version is not null)
            await _permissions.RequirePermissionAsync(fileNodeId, caller, SharePermission.Read, cancellationToken);

        return version is null ? null : ToDto(version);
    }

    private static FileVersionDto ToDto(FileVersion v) => new()
    {
        Id = v.Id,
        VersionNumber = v.VersionNumber,
        Size = v.Size,
        ContentHash = v.ContentHash,
        MimeType = v.MimeType,
        CreatedByUserId = v.CreatedByUserId,
        CreatedAt = v.CreatedAt,
        Label = v.Label
    };
}
