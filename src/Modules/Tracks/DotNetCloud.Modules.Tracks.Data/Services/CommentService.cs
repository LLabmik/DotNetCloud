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
