using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for tracking watch progress — resume position per user per video.
/// </summary>
public sealed class WatchProgressService : IWatchProgressService
{
    private readonly VideoDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<WatchProgressService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchProgressService"/> class.
    /// </summary>
    public WatchProgressService(VideoDbContext db, IEventBus eventBus, ILogger<WatchProgressService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Updates watch progress for a video. Creates or updates the progress record.
    /// </summary>
    public async Task UpdateProgressAsync(Guid videoId, UpdateWatchProgressDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var progress = await _db.WatchProgresses
            .FirstOrDefaultAsync(wp => wp.VideoId == videoId && wp.UserId == caller.UserId, cancellationToken);

        if (progress is null)
        {
            progress = new WatchProgress
            {
                UserId = caller.UserId,
                VideoId = videoId,
                PositionTicks = dto.PositionTicks
            };
            _db.WatchProgresses.Add(progress);
        }
        else
        {
            progress.PositionTicks = dto.PositionTicks;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        // Mark as completed if watched >= 90%
        if (video.DurationTicks > 0)
        {
            progress.IsCompleted = (double)dto.PositionTicks / video.DurationTicks >= 0.9;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets watch progress for a specific video.
    /// </summary>
    public async Task<WatchProgressDto?> GetProgressAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var progress = await _db.WatchProgresses
            .Include(wp => wp.Video)
            .FirstOrDefaultAsync(wp => wp.VideoId == videoId && wp.UserId == caller.UserId, cancellationToken);

        return progress?.Video is null ? null : MapToDto(progress);
    }

    /// <summary>
    /// Gets all in-progress videos for "Continue Watching".
    /// </summary>
    public async Task<IReadOnlyList<WatchProgressDto>> GetContinueWatchingAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var progresses = await _db.WatchProgresses
            .Include(wp => wp.Video)
            .Where(wp => wp.UserId == caller.UserId && !wp.IsCompleted && wp.PositionTicks > 0)
            .OrderByDescending(wp => wp.UpdatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return progresses
            .Where(wp => wp.Video is not null)
            .Select(MapToDto)
            .ToList();
    }

    /// <summary>
    /// Records a view (increments view count, adds watch history, publishes event).
    /// </summary>
    public async Task RecordViewAsync(Guid videoId, CallerContext caller, int durationWatchedSeconds = 0, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        video.ViewCount++;
        video.UpdatedAt = DateTime.UtcNow;

        _db.WatchHistories.Add(new WatchHistory
        {
            UserId = caller.UserId,
            VideoId = videoId,
            DurationWatchedSeconds = durationWatchedSeconds
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new VideoWatchedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            VideoId = videoId,
            UserId = caller.UserId,
            DurationWatchedSeconds = durationWatchedSeconds
        }, caller, cancellationToken);

        _logger.LogInformation("View recorded for video {VideoId} by user {UserId}", videoId, caller.UserId);
    }

    /// <summary>
    /// Gets watch history for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<WatchHistory>> GetWatchHistoryAsync(Guid userId, int count = 20, CancellationToken cancellationToken = default)
    {
        return await _db.WatchHistories
            .Include(wh => wh.Video)
            .Where(wh => wh.UserId == userId)
            .OrderByDescending(wh => wh.WatchedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private static WatchProgressDto MapToDto(WatchProgress progress)
    {
        var durationTicks = progress.Video?.DurationTicks ?? 0;
        var percent = durationTicks > 0
            ? Math.Round((double)progress.PositionTicks / durationTicks * 100, 1)
            : 0;

        return new WatchProgressDto
        {
            VideoId = progress.VideoId,
            VideoTitle = progress.Video?.Title ?? "Unknown",
            PositionTicks = progress.PositionTicks,
            DurationTicks = durationTicks,
            ProgressPercent = percent,
            LastWatchedAt = progress.UpdatedAt
        };
    }
}
