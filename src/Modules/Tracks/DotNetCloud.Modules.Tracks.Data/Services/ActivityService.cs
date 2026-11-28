using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ActivityService
{
    private readonly TracksDbContext _db;
    private readonly IUserDirectory _userDirectory;

    public ActivityService(TracksDbContext db, IUserDirectory userDirectory)
    {
        _db = db;
        _userDirectory = userDirectory;
    }

    public async Task WriteActivityAsync(Guid productId, Guid userId, string action, string entityType, Guid entityId, string? details, CancellationToken ct)
    {
        var activity = new Activity
        {
            ProductId = productId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);
    }

    // ── Convenience Methods for Deletion Lifecycle ────────────────────

    public async Task WriteWorkItemDeletedActivityAsync(Guid productId, Guid userId, Guid workItemId, string title, CancellationToken ct)
    {
        var details = JsonSerializer.Serialize(new { title });
        await WriteActivityAsync(productId, userId, "workitem.deleted", "WorkItem", workItemId, details, ct);
    }

    public async Task WriteWorkItemRestoredActivityAsync(Guid productId, Guid userId, Guid workItemId, string title, CancellationToken ct)
    {
        var details = JsonSerializer.Serialize(new { title });
        await WriteActivityAsync(productId, userId, "workitem.restored", "WorkItem", workItemId, details, ct);
    }

    public async Task WriteWorkItemPermanentDeletedActivityAsync(Guid productId, Guid userId, Guid workItemId, string title, CancellationToken ct)
    {
        var details = JsonSerializer.Serialize(new { title });
        await WriteActivityAsync(productId, userId, "workitem.permanent_deleted", "WorkItem", workItemId, details, ct);
    }

    public async Task WriteCommentDeletedActivityAsync(Guid productId, Guid userId, Guid workItemId, Guid commentId, CancellationToken ct)
    {
        await WriteActivityAsync(productId, userId, "comment.deleted", "Comment", commentId, null, ct);
    }

    public async Task WriteCommentRestoredActivityAsync(Guid productId, Guid userId, Guid workItemId, Guid commentId, CancellationToken ct)
    {
        await WriteActivityAsync(productId, userId, "comment.restored", "Comment", commentId, null, ct);
    }

    public async Task WriteTrashEmptiedActivityAsync(Guid productId, Guid userId, int count, CancellationToken ct)
    {
        var details = JsonSerializer.Serialize(new { count });
        await WriteActivityAsync(productId, userId, "trash.emptied", "Product", productId, details, ct);
    }

    // ── Queries with DisplayName Resolution ───────────────────────────

    public async Task<List<ActivityDto>> GetActivitiesByProductAsync(Guid productId, int skip, int take, CancellationToken ct)
    {
        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.ProductId == productId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return await ResolveDisplayNamesAsync(activities, ct);
    }

    public async Task<List<ActivityDto>> GetActivitiesByWorkItemAsync(Guid workItemId, int skip, int take, CancellationToken ct)
    {
        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.EntityType == "WorkItem" && a.EntityId == workItemId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return await ResolveDisplayNamesAsync(activities, ct);
    }

    private async Task<List<ActivityDto>> ResolveDisplayNamesAsync(List<Activity> activities, CancellationToken ct)
    {
        if (activities.Count == 0)
            return [];

        // Collect unique user IDs across all activities
        var userIds = activities
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        // Resolve display names from IUserDirectory
        var displayNames = await _userDirectory.GetDisplayNamesAsync(userIds, ct);

        return activities.Select(a => new ActivityDto
        {
            Id = a.Id,
            ProductId = a.ProductId,
            UserId = a.UserId,
            DisplayName = displayNames.GetValueOrDefault(a.UserId),
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Details = a.Details,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}
