using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ActivityService
{
    private readonly TracksDbContext _db;

    public ActivityService(TracksDbContext db)
    {
        _db = db;
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

    public async Task<List<ActivityDto>> GetActivitiesByProductAsync(Guid productId, int skip, int take, CancellationToken ct)
    {
        return await _db.Activities
            .AsNoTracking()
            .Where(a => a.ProductId == productId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                ProductId = a.ProductId,
                UserId = a.UserId,
                DisplayName = null,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<ActivityDto>> GetActivitiesByWorkItemAsync(Guid workItemId, int skip, int take, CancellationToken ct)
    {
        return await _db.Activities
            .AsNoTracking()
            .Where(a => a.EntityType == "WorkItem" && a.EntityId == workItemId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                ProductId = a.ProductId,
                UserId = a.UserId,
                DisplayName = null,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);
    }
}
