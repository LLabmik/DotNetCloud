using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing videos — CRUD, search, recently watched, favorites.
/// </summary>
public sealed class VideoService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoService"/> class.
    /// </summary>
    public VideoService(VideoDbContext db, ILogger<VideoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets a video by ID.
    /// </summary>
    public async Task<VideoDto?> GetVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .Include(v => v.Metadata)
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken);

        return video is null ? null : MapToDto(video, caller.UserId);
    }

    /// <summary>
    /// Lists videos for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> ListVideosAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapToDto(v, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches videos by title.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId && v.Title.Contains(query))
            .OrderBy(v => v.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapToDto(v, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets recently added videos.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetRecentVideosAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId)
            .OrderByDescending(v => v.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapToDto(v, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets favorite videos.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId && v.IsFavorite)
            .OrderByDescending(v => v.UpdatedAt)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapToDto(v, caller.UserId)).ToList();
    }

    /// <summary>
    /// Toggles favorite status on a video.
    /// </summary>
    public async Task<bool> ToggleFavoriteAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        video.IsFavorite = !video.IsFavorite;
        video.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} favorite toggled to {IsFavorite} by user {UserId}",
            videoId, video.IsFavorite, caller.UserId);

        return video.IsFavorite;
    }

    /// <summary>
    /// Soft-deletes a video.
    /// </summary>
    public async Task DeleteVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        video.IsDeleted = true;
        video.DeletedAt = DateTime.UtcNow;
        video.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} soft-deleted by user {UserId}", videoId, caller.UserId);
    }

    internal VideoDto MapToDto(Models.Video video, Guid userId)
    {
        var watchProgress = _db.WatchProgresses
            .FirstOrDefault(wp => wp.VideoId == video.Id && wp.UserId == userId);

        return new VideoDto
        {
            Id = video.Id,
            FileNodeId = video.FileNodeId,
            Title = video.Title,
            FileName = video.FileName,
            MimeType = video.MimeType,
            SizeBytes = video.SizeBytes,
            Duration = TimeSpan.FromTicks(video.DurationTicks),
            Width = video.Metadata?.Width,
            Height = video.Metadata?.Height,
            IsFavorite = video.IsFavorite,
            ViewCount = video.ViewCount,
            WatchPositionTicks = watchProgress?.PositionTicks,
            CreatedAt = video.CreatedAt
        };
    }
}
