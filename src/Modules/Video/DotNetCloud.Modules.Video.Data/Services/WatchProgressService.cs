using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video watch progress and resume playback.
/// </summary>
public sealed class WatchProgressService : IWatchProgressService
{
    private static readonly TimeSpan ResetThreshold = TimeSpan.FromMinutes(5);

    private readonly VideoDbContext _db;
    private readonly ILogger<WatchProgressService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchProgressService"/> class.
    /// </summary>
    public WatchProgressService(VideoDbContext db, ILogger<WatchProgressService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WatchProgressDto?> GetProgressAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo?.CanonicalVideo is null || userVideo.WatchPositionTicks is null)
            return null;

        var positionTicks = userVideo.WatchPositionTicks.Value;
        var durationTicks = userVideo.CanonicalVideo.DurationTicks;

        // Apply resume logic: if within first or last 5 minutes, treat as no progress
        if (ShouldResetPosition(positionTicks, durationTicks))
            return null;

        return new WatchProgressDto
        {
            VideoId = userVideo.Id,
            VideoTitle = userVideo.CanonicalVideo.Title,
            PositionTicks = positionTicks,
            DurationTicks = durationTicks,
            ProgressPercent = durationTicks > 0 ? (double)positionTicks / durationTicks * 100 : 0,
            LastWatchedAt = userVideo.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(Guid videoId, UpdateWatchProgressDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo?.CanonicalVideo is null)
        {
            _logger.LogWarning("UpdateProgress: video {VideoId} not found for user {UserId}", videoId, caller.UserId);
            return;
        }

        var durationTicks = userVideo.CanonicalVideo.DurationTicks;

        // If position is within first or last 5 minutes, reset to null (no progress)
        if (ShouldResetPosition(dto.PositionTicks, durationTicks))
        {
            userVideo.WatchPositionTicks = null;
        }
        else
        {
            userVideo.WatchPositionTicks = dto.PositionTicks;
        }

        userVideo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Watch progress for video {VideoId} (user {UserId}): {PositionTicks} ticks",
            videoId, caller.UserId, userVideo.WatchPositionTicks);
    }

    /// <summary>
    /// Applies resume logic: determines whether a watch position should be treated as "no progress"
    /// (start from beginning) based on the first/last 5 minute rule.
    /// </summary>
    public static long? ApplyResumeLogic(long? watchPositionTicks, long durationTicks)
    {
        if (watchPositionTicks is null || durationTicks <= 0)
            return null;

        if (ShouldResetPosition(watchPositionTicks.Value, durationTicks))
            return null;

        return watchPositionTicks;
    }

    private static bool ShouldResetPosition(long positionTicks, long durationTicks)
    {
        if (durationTicks <= 0)
            return true;

        var resetThresholdTicks = ResetThreshold.Ticks;

        // Within first 5 minutes
        if (positionTicks <= resetThresholdTicks)
            return true;

        // Within last 5 minutes
        if (positionTicks >= durationTicks - resetThresholdTicks)
            return true;

        return false;
    }
}
