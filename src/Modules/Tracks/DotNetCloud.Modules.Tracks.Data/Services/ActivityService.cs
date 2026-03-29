using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for logging and querying board activity (audit log).
/// </summary>
public sealed class ActivityService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<ActivityService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityService"/> class.
    /// </summary>
    public ActivityService(TracksDbContext db, ILogger<ActivityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Logs an activity entry for a board.
    /// </summary>
    public async Task LogAsync(Guid boardId, Guid userId, string action, string entityType, Guid entityId, string? details = null, CancellationToken cancellationToken = default)
    {
        var activity = new BoardActivity
        {
            BoardId = boardId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        };

        _db.BoardActivities.Add(activity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the activity feed for a board, ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<BoardActivityDto>> GetBoardActivityAsync(Guid boardId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _db.BoardActivities
            .AsNoTracking()
            .Where(a => a.BoardId == boardId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return activities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets activity for a specific card.
    /// </summary>
    public async Task<IReadOnlyList<BoardActivityDto>> GetCardActivityAsync(Guid cardId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var activities = await _db.BoardActivities
            .AsNoTracking()
            .Where(a => a.EntityType == "Card" && a.EntityId == cardId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return activities.Select(MapToDto).ToList();
    }

    private static BoardActivityDto MapToDto(BoardActivity a) => new()
    {
        Id = a.Id,
        BoardId = a.BoardId,
        UserId = a.UserId,
        Action = a.Action,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        Details = a.Details,
        CreatedAt = a.CreatedAt
    };
}
