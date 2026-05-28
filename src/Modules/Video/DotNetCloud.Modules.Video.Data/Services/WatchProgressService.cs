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
    /// Resolves canonical (UserVideo) IDs to old Video IDs for FK constraint compatibility.
    /// </summary>
    public async Task UpdateProgressAsync(Guid videoId, UpdateWatchProgressDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var oldVideoId = await ResolveOldVideoIdAsync(videoId, caller.UserId, cancellationToken);
        if (oldVideoId is null)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == oldVideoId.Value && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var progress = await _db.WatchProgresses
            .FirstOrDefaultAsync(wp => wp.VideoId == oldVideoId.Value && wp.UserId == caller.UserId, cancellationToken);

        if (progress is null)
        {
            progress = new WatchProgress
            {
                UserId = caller.UserId,
                VideoId = oldVideoId.Value,
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
    /// Tries the given video ID directly (backward compat), then resolves canonical IDs.
    /// </summary>
    public async Task<WatchProgressDto?> GetProgressAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try direct lookup first (old Video.Id path)
        var progress = await _db.WatchProgresses
            .Include(wp => wp.Video)
            .FirstOrDefaultAsync(wp => wp.VideoId == videoId && wp.UserId == caller.UserId, cancellationToken);

        if (progress?.Video is not null)
            return MapToDto(progress);

        // Resolve canonical (UserVideo.Id) to old Video.Id and retry
        var oldVideoId = await ResolveOldVideoIdAsync(videoId, caller.UserId, cancellationToken);
        if (oldVideoId.HasValue && oldVideoId.Value != videoId)
        {
            progress = await _db.WatchProgresses
                .Include(wp => wp.Video)
                .FirstOrDefaultAsync(wp => wp.VideoId == oldVideoId.Value && wp.UserId == caller.UserId, cancellationToken);
        }

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
    /// Resolves canonical (UserVideo) IDs to old Video IDs for FK constraint compatibility.
    /// </summary>
    public async Task RecordViewAsync(Guid videoId, CallerContext caller, int durationWatchedSeconds = 0, CancellationToken cancellationToken = default)
    {
        var oldVideoId = await ResolveOldVideoIdAsync(videoId, caller.UserId, cancellationToken);
        if (oldVideoId is null)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == oldVideoId.Value && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        video.ViewCount++;
        video.UpdatedAt = DateTime.UtcNow;

        // Also increment UserVideo view count if using canonical path
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);
        if (userVideo is not null)
        {
            userVideo.ViewCount++;
            userVideo.UpdatedAt = DateTime.UtcNow;
        }

        _db.WatchHistories.Add(new WatchHistory
        {
            UserId = caller.UserId,
            VideoId = oldVideoId.Value,
            DurationWatchedSeconds = durationWatchedSeconds
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new VideoWatchedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            VideoId = oldVideoId.Value,
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

    /// <summary>
    /// Resolves a user-facing video ID to the old Video.Id value required
    /// by the WatchProgress.VideoId FK constraint (references old Videos table).
    /// Checks the old Videos table first (backward compat), then resolves via UserVideos → FileNodeId.
    /// </summary>
    private async Task<Guid?> ResolveOldVideoIdAsync(Guid videoId, Guid userId, CancellationToken cancellationToken)
    {
        // Direct match in old Videos table (backward compat — videoId is already an old Video.Id)
        var oldVideo = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == userId, cancellationToken);
        if (oldVideo is not null)
            return oldVideo.Id;

        // Canonical path: videoId is a UserVideo.Id — resolve to old Video record via FileNodeId
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == userId, cancellationToken);
        if (userVideo is not null)
        {
            var matchingOld = await _db.Videos
                .FirstOrDefaultAsync(v => v.FileNodeId == userVideo.FileNodeId && v.OwnerId == userId, cancellationToken);
            if (matchingOld is not null)
                return matchingOld.Id;
        }

        return null;
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
