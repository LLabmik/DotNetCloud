using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Manages threaded comments on files and folders.
/// </summary>
internal sealed class CommentService : ICommentService
{
    private readonly FilesDbContext _db;
    private readonly ILogger<CommentService> _logger;

    public CommentService(FilesDbContext db, ILogger<CommentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FileCommentDto> AddCommentAsync(Guid fileNodeId, string content, Guid? parentCommentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _ = await _db.FileNodes.FindAsync([fileNodeId], cancellationToken)
            ?? throw new NotFoundException("FileNode", fileNodeId);

        if (parentCommentId.HasValue)
        {
            var parent = await _db.FileComments
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentCommentId.Value && c.FileNodeId == fileNodeId, cancellationToken)
                ?? throw new NotFoundException("FileComment", parentCommentId.Value);
        }

        var comment = new FileComment
        {
            FileNodeId = fileNodeId,
            Content = content,
            ParentCommentId = parentCommentId,
            CreatedByUserId = caller.UserId
        };

        _db.FileComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment {CommentId} added to node {NodeId} by {UserId}",
            comment.Id, fileNodeId, caller.UserId);

        return ToDto(comment, 0);
    }

    /// <inheritdoc />
    public async Task<FileCommentDto> EditCommentAsync(Guid commentId, string content, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var comment = await _db.FileComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("FileComment", commentId);

        if (comment.CreatedByUserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("Only the comment author or a system caller can edit this comment.");

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var replyCount = await _db.FileComments
            .CountAsync(c => c.ParentCommentId == commentId, cancellationToken);

        return ToDto(comment, replyCount);
    }

    /// <inheritdoc />
    public async Task DeleteCommentAsync(Guid commentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var comment = await _db.FileComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("FileComment", commentId);

        if (comment.CreatedByUserId != caller.UserId && caller.Type != CallerType.System)
            throw new ForbiddenException("Only the comment author or a system caller can delete this comment.");

        comment.IsDeleted = true;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comment {CommentId} soft-deleted by {UserId}", commentId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileCommentDto>> GetCommentsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var comments = await _db.FileComments
            .AsNoTracking()
            .Where(c => c.FileNodeId == fileNodeId && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var commentIds = comments.Select(c => c.Id).ToList();
        var replyCounts = await _db.FileComments
            .Where(c => c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value))
            .GroupBy(c => c.ParentCommentId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count, cancellationToken);

        return comments.Select(c => ToDto(c, replyCounts.GetValueOrDefault(c.Id, 0))).ToList();
    }

    /// <inheritdoc />
    public async Task<FileCommentDto?> GetCommentAsync(Guid commentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var comment = await _db.FileComments
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);

        if (comment is null)
            return null;

        var replyCount = await _db.FileComments
            .CountAsync(c => c.ParentCommentId == commentId, cancellationToken);

        return ToDto(comment, replyCount);
    }

    private static FileCommentDto ToDto(FileComment comment, int replyCount) => new()
    {
        Id = comment.Id,
        FileNodeId = comment.FileNodeId,
        ParentCommentId = comment.ParentCommentId,
        Content = comment.Content,
        CreatedByUserId = comment.CreatedByUserId,
        CreatedAt = comment.CreatedAt,
        UpdatedAt = comment.UpdatedAt,
        ReplyCount = replyCount
    };
}
