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
/// Implementation of <see cref="ISyncFolderRegistrationService"/> backed by the Files
/// module database.
/// </summary>
internal sealed class SyncFolderRegistrationService : ISyncFolderRegistrationService
{
    private readonly FilesDbContext _db;
    private readonly ILogger<SyncFolderRegistrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncFolderRegistrationService"/> class.
    /// </summary>
    public SyncFolderRegistrationService(FilesDbContext db, ILogger<SyncFolderRegistrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncFolderRegistrationDto>> ListAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.SyncFolderRegistrations
            .AsNoTracking()
            .Where(r => r.UserId == caller.UserId && r.IsActive)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new SyncFolderRegistrationDto
            {
                Id = r.Id,
                RemoteFolderNodeId = r.RemoteFolderNodeId,
                RemoteFolderPath = r.RemoteFolderPath,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SyncFolderRegistrationDto> RegisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == remoteFolderNodeId, cancellationToken)
            ?? throw new NotFoundException("FileNode", remoteFolderNodeId);

        if (folder.OwnerId != caller.UserId)
        {
            throw new ForbiddenException("The requested folder does not belong to the current user.");
        }

        if (folder.NodeType != FileNodeType.Folder)
        {
            throw new ValidationException("remoteFolderNodeId", "The sync target must be a folder, not a file.");
        }

        // Remote overlap check: reject a folder that is equal to, a descendant of, or an
        // ancestor of an already-registered sync folder.
        var existing = await _db.SyncFolderRegistrations
            .AsNoTracking()
            .Where(r => r.UserId == caller.UserId && r.IsActive)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            var existingIds = existing.Select(e => e.RemoteFolderNodeId).ToHashSet();
            var existingNodes = await _db.FileNodes
                .AsNoTracking()
                .Where(n => n.OwnerId == caller.UserId && existingIds.Contains(n.Id))
                .ToListAsync(cancellationToken);

            foreach (var reg in existing)
            {
                var node = existingNodes.FirstOrDefault(n => n.Id == reg.RemoteFolderNodeId);
                if (node is null)
                {
                    continue;
                }

                if (node.Id == folder.Id)
                {
                    // Idempotent: re-registering the same folder returns the existing registration.
                    return ToDto(reg);
                }

                if (folder.MaterializedPath.StartsWith(node.MaterializedPath + "/", StringComparison.Ordinal))
                {
                    throw new ValidationException("remoteFolderNodeId", "This folder is inside an already-registered sync folder.");
                }

                if (node.MaterializedPath.StartsWith(folder.MaterializedPath + "/", StringComparison.Ordinal))
                {
                    throw new ValidationException("remoteFolderNodeId", "This folder contains an already-registered sync folder.");
                }
            }
        }

        var registration = new SyncFolderRegistration
        {
            UserId = caller.UserId,
            RemoteFolderNodeId = folder.Id,
            RemoteFolderPath = await BuildRemoteFolderPathAsync(folder, caller.UserId, cancellationToken),
            IsActive = true,
        };

        _db.SyncFolderRegistrations.Add(registration);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueConstraintViolation(ex))
        {
            // Race: another request registered the same folder first. Return the existing row.
            _logger.LogDebug("Sync folder registration raced for folder {FolderId}; returning existing row.", remoteFolderNodeId);
            var existingRow = await _db.SyncFolderRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == caller.UserId && r.RemoteFolderNodeId == folder.Id, cancellationToken);

            if (existingRow is not null)
            {
                return ToDto(existingRow);
            }

            throw;
        }

        return ToDto(registration);
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var reg = await _db.SyncFolderRegistrations
            .FirstOrDefaultAsync(r => r.UserId == caller.UserId && r.RemoteFolderNodeId == remoteFolderNodeId, cancellationToken)
            ?? throw new NotFoundException("SyncFolderRegistration", remoteFolderNodeId);

        reg.IsActive = false;
        reg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SyncFolderRegistrationDto ToDto(SyncFolderRegistration r) => new()
    {
        Id = r.Id,
        RemoteFolderNodeId = r.RemoteFolderNodeId,
        RemoteFolderPath = r.RemoteFolderPath,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private async Task<string> BuildRemoteFolderPathAsync(FileNode folder, Guid userId, CancellationToken cancellationToken)
    {
        var segments = new List<string>();
        var current = folder;

        while (current is not null)
        {
            segments.Add(current.Name);
            if (current.ParentId is null)
            {
                break;
            }

            current = await _db.FileNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == current.ParentId.Value && n.OwnerId == userId, cancellationToken);
        }

        segments.Reverse();
        return "/" + string.Join("/", segments);
    }
}
