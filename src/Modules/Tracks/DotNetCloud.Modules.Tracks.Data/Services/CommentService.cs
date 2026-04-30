using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class CommentService
{
    private readonly TracksDbContext _db;

    public CommentService(TracksDbContext db)
    {
        _db = db;
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
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
