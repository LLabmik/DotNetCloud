using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages file/folder tags backed by the Files database.
/// </summary>
internal sealed class TagService : ITagService
{
    private readonly FilesDbContext _db;
    private readonly ILogger<TagService> _logger;

    public TagService(FilesDbContext db, ILogger<TagService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileTagDto> AddTagAsync(Guid fileNodeId, string name, string? color, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var node = await _db.FileNodes.FindAsync([fileNodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        EnsureOwnerOrSystem(node, caller);

        var duplicate = await _db.FileTags
            .AnyAsync(t => t.FileNodeId == fileNodeId && t.Name == name && t.CreatedByUserId == caller.UserId, cancellationToken);

        if (duplicate)
            throw new Core.Errors.ValidationException("Name", $"Tag '{name}' already exists on this node.");

        var tag = new FileTag
        {
            FileNodeId = fileNodeId,
            Name = name,
            Color = color,
            CreatedByUserId = caller.UserId
        };

        _db.FileTags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag '{TagName}' added to node {NodeId} by {UserId}", name, fileNodeId, caller.UserId);

        return ToDto(tag);
    }

    /// <inheritdoc />
    public async Task RemoveTagAsync(Guid fileNodeId, Guid tagId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var tag = await _db.FileTags
            .FirstOrDefaultAsync(t => t.Id == tagId && t.FileNodeId == fileNodeId, cancellationToken)
            ?? throw new NotFoundException("FileTag", tagId);

        if (tag.CreatedByUserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("Only the tag creator or a system caller can remove this tag.");

        _db.FileTags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} removed from node {NodeId} by {UserId}", tagId, fileNodeId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileTagDto>> GetTagsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileTags
            .AsNoTracking()
            .Where(t => t.FileNodeId == fileNodeId)
            .OrderBy(t => t.Name)
            .Select(t => new FileTagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileNodeDto>> GetNodesByTagAsync(string tagName, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        return await _db.FileTags
            .AsNoTracking()
            .Where(t => t.Name == tagName && t.CreatedByUserId == caller.UserId)
            .Select(t => t.FileNode!)
            .Select(n => new FileNodeDto
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType.ToString(),
                MimeType = n.MimeType,
                Size = n.Size,
                ParentId = n.ParentId,
                OwnerId = n.OwnerId,
                CurrentVersion = n.CurrentVersion,
                IsFavorite = n.IsFavorite,
                ContentHash = n.ContentHash,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllUserTagsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileTags
            .AsNoTracking()
            .Where(t => t.CreatedByUserId == caller.UserId)
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveTagByNameAsync(Guid fileNodeId, string tagName, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var tag = await _db.FileTags
            .FirstOrDefaultAsync(t => t.FileNodeId == fileNodeId && t.Name == tagName && t.CreatedByUserId == caller.UserId, cancellationToken)
            ?? throw new NotFoundException("FileTag", tagName);

        _db.FileTags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag '{TagName}' removed from node {NodeId} by {UserId}", tagName, fileNodeId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserTagSummaryDto>> GetUserTagSummariesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return await _db.FileTags
            .AsNoTracking()
            .Where(t => t.CreatedByUserId == caller.UserId)
            .GroupBy(t => t.Name)
            .Select(g => new UserTagSummaryDto
            {
                Name = g.Key,
                Color = g.OrderByDescending(t => t.CreatedAt).Select(t => t.Color).FirstOrDefault(),
                FileCount = g.Count()
            })
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BulkResultDto> BulkAddTagAsync(IReadOnlyList<Guid> nodeIds, string tagName, string? color, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var results = new List<BulkItemResultDto>(nodeIds.Count);

        foreach (var nodeId in nodeIds)
        {
            try
            {
                await AddTagAsync(nodeId, tagName, color, caller, cancellationToken);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return new BulkResultDto
        {
            TotalCount = nodeIds.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };
    }

    /// <inheritdoc />
    public async Task<BulkResultDto> BulkRemoveTagByNameAsync(IReadOnlyList<Guid> nodeIds, string tagName, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        var results = new List<BulkItemResultDto>(nodeIds.Count);

        foreach (var nodeId in nodeIds)
        {
            try
            {
                await RemoveTagByNameAsync(nodeId, tagName, caller, cancellationToken);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }

        return new BulkResultDto
        {
            TotalCount = nodeIds.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };
    }

    private static void EnsureOwnerOrSystem(FileNode node, CallerContext caller)
    {
        if (caller.Type == CallerType.System)
            return;

        if (node.OwnerId != caller.UserId)
            throw new ForbiddenException("You do not have permission to modify tags on this node.");
    }

    private static FileTagDto ToDto(FileTag tag) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        Color = tag.Color,
        CreatedAt = tag.CreatedAt
    };
}
