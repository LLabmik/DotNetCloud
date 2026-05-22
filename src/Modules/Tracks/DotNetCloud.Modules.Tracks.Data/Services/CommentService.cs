using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class CommentService
{
    private readonly TracksDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ActivityService _activityService;

    public CommentService(TracksDbContext db, IEventBus eventBus, ActivityService activityService)
    {
        _db = db;
        _eventBus = eventBus;
        _activityService = activityService;
    }

    public async Task<WorkItemCommentDto> CreateCommentAsync(
        Guid workItemId,
        Guid userId,
        AddWorkItemCommentDto dto,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var comment = new WorkItemComment
        {
            WorkItemId = workItemId,
            UserId = userId,
            Content = dto.Content,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.WorkItemComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return Map(comment);
    }

    public async Task<List<WorkItemCommentDto>> GetCommentsByWorkItemAsync(
        Guid workItemId,
        int skip,
        int take,
        CancellationToken ct)
    {
        var comments = await _db.WorkItemComments
            .IgnoreQueryFilters()
            .Where(c => c.WorkItemId == workItemId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(c => Map(c))
            .ToListAsync(ct);

        return comments;
    }

    public async Task<WorkItemCommentDto> UpdateCommentAsync(
        Guid commentId,
        Guid userId,
        UpdateWorkItemCommentDto dto,
        CancellationToken ct)
    {
        var comment = await _db.WorkItemComments.FindAsync(new object[] { commentId }, ct);

        if (comment is null || comment.UserId != userId)
        {
            throw new InvalidOperationException("Comment not found or not authorized to edit.");
        }

        comment.Content = dto.Content;
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Map(comment);
    }

    public async Task DeleteCommentAsync(Guid commentId, Guid userId, CancellationToken ct)
    {
        var comment = await _db.WorkItemComments.FindAsync(new object[] { commentId }, ct);

        if (comment is null || comment.UserId != userId)
        {
            throw new InvalidOperationException("Comment not found or not authorized to delete.");
        }

        var now = DateTime.UtcNow;
        comment.IsDeleted = true;
        comment.DeletedAt = now;
        comment.UpdatedAt = now;
        comment.DeletedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        // Record activity
        await _activityService.WriteCommentDeletedActivityAsync(comment.WorkItemId, userId, comment.WorkItemId, commentId, ct);

        // Publish comment deletion event
        var caller = new CallerContext(userId, Array.Empty<string>(), CallerType.User);
        await _eventBus.PublishAsync(new WorkItemCommentDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = comment.WorkItemId,
            CommentId = commentId,
            UserId = userId
        }, caller, ct);
    }

    /// <summary>
    /// Lists soft-deleted comments for a work item.
    /// </summary>
    public async Task<List<WorkItemCommentDto>> ListDeletedCommentsAsync(Guid workItemId, CancellationToken ct)
    {
        return await _db.WorkItemComments
            .IgnoreQueryFilters()
            .Where(c => c.WorkItemId == workItemId && c.IsDeleted)
            .OrderByDescending(c => c.DeletedAt)
            .Select(c => Map(c))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Restores a soft-deleted comment.
    /// </summary>
    public async Task RestoreCommentAsync(Guid commentId, Guid userId, CancellationToken ct)
    {
        var comment = await _db.WorkItemComments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IsDeleted, ct)
            ?? throw new InvalidOperationException("Deleted comment not found.");

        if (comment.UserId != userId)
        {
            // Allow admins to restore any comment; only author can restore their own
            throw new InvalidOperationException("Not authorized to restore this comment.");
        }

        comment.IsDeleted = false;
        comment.DeletedAt = null;
        comment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Permanently deletes a comment.
    /// </summary>
    public async Task PermanentDeleteCommentAsync(Guid commentId, Guid userId, CancellationToken ct)
    {
        var comment = await _db.WorkItemComments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new InvalidOperationException("Comment not found.");

        if (comment.UserId != userId)
        {
            throw new InvalidOperationException("Not authorized to permanently delete this comment.");
        }

        _db.WorkItemComments.Remove(comment);
        await _db.SaveChangesAsync(ct);
    }

    // ── Reactions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an emoji reaction from a user to a comment. If the user already reacted
    /// with the same emoji, the operation is a no-op (idempotent).
    /// </summary>
    public async Task<CommentReactionDto> AddReactionAsync(
        Guid commentId,
        Guid userId,
        string emoji,
        CancellationToken ct)
    {
        // Verify comment exists
        var commentExists = await _db.WorkItemComments
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Id == commentId && !c.IsDeleted, ct);

        if (!commentExists)
            throw new InvalidOperationException("Comment not found.");

        // Check for existing reaction (composite key)
        var existing = await _db.CommentReactions
            .FindAsync(new object[] { commentId, userId, emoji }, ct);

        if (existing is not null)
        {
            return MapReaction(existing);
        }

        var reaction = new CommentReaction
        {
            CommentId = commentId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = DateTime.UtcNow
        };

        _db.CommentReactions.Add(reaction);
        await _db.SaveChangesAsync(ct);

        return MapReaction(reaction);
    }

    /// <summary>
    /// Removes a user's emoji reaction from a comment.
    /// </summary>
    public async Task RemoveReactionAsync(
        Guid commentId,
        Guid userId,
        string emoji,
        CancellationToken ct)
    {
        var reaction = await _db.CommentReactions
            .FindAsync(new object[] { commentId, userId, emoji }, ct);

        if (reaction is null)
            throw new InvalidOperationException("Reaction not found.");

        _db.CommentReactions.Remove(reaction);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets all reactions for a comment, grouped by emoji with counts.
    /// </summary>
    public async Task<List<CommentReactionSummaryDto>> GetReactionsAsync(
        Guid commentId,
        Guid? currentUserId,
        CancellationToken ct)
    {
        var reactions = await _db.CommentReactions
            .Where(r => r.CommentId == commentId)
            .ToListAsync(ct);

        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new CommentReactionSummaryDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                ReactedByCurrentUser = currentUserId.HasValue
                    && g.Any(r => r.UserId == currentUserId.Value)
            })
            .OrderByDescending(s => s.Count)
            .ToList();
    }

    /// <summary>
    /// Gets all reactions across multiple comments (for batch loading).
    /// Returns a dictionary keyed by CommentId.
    /// </summary>
    public async Task<Dictionary<Guid, List<CommentReactionSummaryDto>>> GetReactionsForCommentsAsync(
        IEnumerable<Guid> commentIds,
        Guid? currentUserId,
        CancellationToken ct)
    {
        var idSet = commentIds.ToHashSet();
        if (idSet.Count == 0)
            return new Dictionary<Guid, List<CommentReactionSummaryDto>>();

        var reactions = await _db.CommentReactions
            .Where(r => idSet.Contains(r.CommentId))
            .ToListAsync(ct);

        return reactions
            .GroupBy(r => r.CommentId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Emoji)
                    .Select(eg => new CommentReactionSummaryDto
                    {
                        Emoji = eg.Key,
                        Count = eg.Count(),
                        ReactedByCurrentUser = currentUserId.HasValue
                            && eg.Any(r => r.UserId == currentUserId.Value)
                    })
                    .OrderByDescending(s => s.Count)
                    .ToList()
            );
    }

    private static CommentReactionDto MapReaction(CommentReaction r) => new()
    {
        CommentId = r.CommentId,
        UserId = r.UserId,
        Emoji = r.Emoji,
        CreatedAt = r.CreatedAt
    };

    private static WorkItemCommentDto Map(WorkItemComment c) => new()
    {
        Id = c.Id,
        WorkItemId = c.WorkItemId,
        UserId = c.UserId,
        Content = c.Content,
        IsEdited = c.IsEdited,
        IsDeleted = c.IsDeleted,
        DeletedAt = c.DeletedAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
