using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing videos — CRUD, search, recently watched, favorites.
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly VideoDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IVideoSeriesService _seriesService;
    private readonly ILogger<VideoService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoService"/> class.
    /// </summary>
    public VideoService(VideoDbContext db, IEventBus eventBus, IVideoSeriesService seriesService, ILogger<VideoService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _seriesService = seriesService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new video record linked to a FileNode.
    /// </summary>
    public async Task<VideoDto> CreateVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Videos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.FileNodeId == fileNodeId && v.OwnerId == ownerId && !v.IsDeleted, cancellationToken);

        if (existing is not null)
        {
            _logger.LogDebug("Video already exists for FileNode {FileNodeId}", fileNodeId);
            return MapToDto(existing, ownerId);
        }

        var video = new Models.Video
        {
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            Title = Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes
        };

        _db.Videos.Add(video);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} created for file {FileNodeId} by user {UserId}", video.Id, fileNodeId, ownerId);

        await _eventBus.PublishAsync(new VideoAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            VideoId = video.Id,
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            FileName = fileName
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "video",
            EntityId = video.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken);

        return MapToDto(video, ownerId);
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
    /// Gets a video by its Files-module FileNodeId.
    /// </summary>
    public async Task<VideoDto?> GetVideoByFileNodeIdAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .Include(v => v.Metadata)
            .FirstOrDefaultAsync(v => v.FileNodeId == fileNodeId && v.OwnerId == caller.UserId, cancellationToken);

        return video is null ? null : MapToDto(video, caller.UserId);
    }

    /// <summary>
    /// Lists videos for the authenticated user. Optionally excludes videos that belong to a series.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> ListVideosAsync(CallerContext caller, int skip = 0, int take = 50, bool excludeSeriesContent = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Models.Video> query = _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId);

        if (excludeSeriesContent)
        {
            var seriesVideoIds = await GetSeriesVideoIdsAsync(cancellationToken);
            query = query.Where(v => !seriesVideoIds.Contains(v.Id));
        }

        var videos = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapToDto(v, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches videos and series by title. Returns series matches + standalone video matches.
    /// Videos that belong to a series are excluded from the video results.
    /// </summary>
    public async Task<VideoSearchResultDto> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new VideoSearchResultDto();

        var searchTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLower())
            .ToList();

        // ── Search series ──
        IQueryable<VideoSeries> seriesQuery = _db.VideoSeries
            .Where(s => s.OwnerId == caller.UserId);

        foreach (var term in searchTerms)
        {
            var capturedTerm = term;
            seriesQuery = seriesQuery.Where(s => s.Name.ToLower().Contains(capturedTerm));
        }

        var matchedSeries = await seriesQuery
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .OrderBy(s => s.Name)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        // ── Search standalone videos (exclude series-linked) ──
        var seriesVideoIds = await GetSeriesVideoIdsAsync(cancellationToken);

        IQueryable<Models.Video> videoQuery = _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId && !seriesVideoIds.Contains(v.Id));

        foreach (var term in searchTerms)
        {
            var capturedTerm = term;
            videoQuery = videoQuery.Where(v => v.Title.ToLower().Contains(capturedTerm));
        }

        var videos = await videoQuery
            .OrderBy(v => v.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return new VideoSearchResultDto
        {
            Series = matchedSeries.Select(s => MapSeriesToDto(s)).ToList(),
            StandaloneVideos = videos.Select(v => MapToDto(v, caller.UserId)).ToList()
        };
    }

    /// <summary>
    /// Gets recently added videos with paging.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetRecentVideosAsync(CallerContext caller, int skip = 0, int take = 20, CancellationToken cancellationToken = default)
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
    /// Gets the total video count for a user. Optionally excludes series-linked videos.
    /// </summary>
    public async Task<int> GetVideoCountAsync(Guid ownerId, bool excludeSeriesContent = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Models.Video> query = _db.Videos.Where(v => v.OwnerId == ownerId);

        if (excludeSeriesContent)
        {
            var seriesVideoIds = await GetSeriesVideoIdsAsync(cancellationToken);
            query = query.Where(v => !seriesVideoIds.Contains(v.Id));
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets favorite videos, excluding videos that belong to a series.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var seriesVideoIds = await GetSeriesVideoIdsAsync(cancellationToken);

        var videos = await _db.Videos
            .Include(v => v.Metadata)
            .Where(v => v.OwnerId == caller.UserId && v.IsFavorite && !seriesVideoIds.Contains(v.Id))
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

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "video",
            EntityId = videoId.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, cancellationToken);
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
            CreatedAt = video.CreatedAt,
            HasExternalPoster = video.HasExternalPoster,
            Overview = video.Overview,
            TmdbRating = video.TmdbRating,
            Genres = video.Genres,
            ReleaseDate = video.ReleaseDate
        };
    }

    /// <summary>
    /// Returns the set of video IDs that belong to any series (as episodes or franchise items).
    /// </summary>
    private async Task<HashSet<Guid>> GetSeriesVideoIdsAsync(CancellationToken cancellationToken = default)
    {
        var episodeVideoIds = await _db.VideoEpisodes
            .Select(e => e.VideoId)
            .ToListAsync(cancellationToken);

        var franchiseVideoIds = await _db.VideoSeriesItems
            .Select(i => i.VideoId)
            .ToListAsync(cancellationToken);

        return episodeVideoIds.Concat(franchiseVideoIds).ToHashSet();
    }

    /// <summary>
    /// Returns combined library content: all series (sorted by name) + paginated standalone videos.
    /// </summary>
    public async Task<VideoLibraryContentDto> ListLibraryContentAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var series = await _seriesService.ListSeriesAsync(caller, cancellationToken);
        var standaloneVideos = await ListVideosAsync(caller, skip, take, excludeSeriesContent: true, cancellationToken);
        var totalStandalone = await GetVideoCountAsync(caller.UserId, excludeSeriesContent: true, cancellationToken);

        return new VideoLibraryContentDto
        {
            Series = series,
            StandaloneVideos = standaloneVideos,
            TotalStandaloneVideos = totalStandalone
        };
    }

    /// <summary>
    /// Maps a VideoSeries entity to a VideoSeriesDto.
    /// </summary>
    private static VideoSeriesDto MapSeriesToDto(VideoSeries series)
    {
        var totalSeasons = series.Seasons?.Count ?? 0;
        var totalEpisodes = series.TotalEpisodes > 0
            ? series.TotalEpisodes
            : series.Items?.Count ?? 0;

        return new VideoSeriesDto
        {
            Id = series.Id,
            Name = series.Name,
            Description = series.Description,
            Type = series.Type.ToString(),
            Year = series.Year,
            TmdbRating = series.TmdbRating,
            Genres = series.Genres,
            Status = series.Status,
            TotalSeasons = totalSeasons,
            TotalEpisodes = totalEpisodes,
            HasExternalPoster = series.HasExternalPoster,
            CreatedAt = series.CreatedAt,
            UpdatedAt = series.UpdatedAt
        };
    }
}
