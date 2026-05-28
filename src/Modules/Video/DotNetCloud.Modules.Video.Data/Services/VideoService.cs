using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Naming;
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
/// Uses canonical/user-junction data model for content deduplication with dual-write
/// to old per-user tables for backward compatibility.
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly VideoDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IVideoSeriesService _seriesService;
    private readonly ITableNamingStrategy _namingStrategy;
    private readonly ILogger<VideoService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoService"/> class.
    /// </summary>
    public VideoService(VideoDbContext db, IEventBus eventBus, IVideoSeriesService seriesService, ITableNamingStrategy namingStrategy, ILogger<VideoService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _seriesService = seriesService;
        _namingStrategy = namingStrategy;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new video record linked to a FileNode.
    /// Uses canonical/user-junction model with dual-write to old table for backward compatibility.
    /// </summary>
    public async Task<VideoDto> CreateVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Check for existing old-format video (backward-compat fast path)
        var existingOld = await _db.Videos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.FileNodeId == fileNodeId && v.OwnerId == ownerId && !v.IsDeleted, cancellationToken);

        if (existingOld is not null)
        {
            _logger.LogDebug("Video already exists for FileNode {FileNodeId}", fileNodeId);
            return MapToDto(existingOld, ownerId);
        }

        // ── ContentHash lookup from FileNode ──
        var contentHash = await LookupContentHashAsync(fileNodeId, cancellationToken);
        var title = Path.GetFileNameWithoutExtension(fileName);

        // ── Canonical video (shared, keyed by ContentHash) ──
        CanonicalVideo? canonical = null;
        if (contentHash is not null)
        {
            canonical = await _db.CanonicalVideos
                .FirstOrDefaultAsync(cv => cv.ContentHash == contentHash, cancellationToken);
        }

        if (canonical is null && contentHash is not null)
        {
            canonical = new CanonicalVideo
            {
                ContentHash = contentHash,
                Title = title,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = sizeBytes
            };
            _db.CanonicalVideos.Add(canonical);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CanonicalVideo {ContentHash} created for file {FileNodeId}", contentHash, fileNodeId);
        }

        // ── User-video junction and dual-write ──
        // When contentHash is available, create canonical + UserVideo junction + old Video (dual-write).
        // When contentHash is null (file not yet hashed), create only old Video record to avoid FK violations.
        UserVideo? userVideo = null;
        Models.Video oldVideo;

        if (contentHash is not null)
        {
            userVideo = new UserVideo
            {
                OwnerId = ownerId,
                FileNodeId = fileNodeId,
                CanonicalContentHash = contentHash
            };
            _db.UserVideos.Add(userVideo);
        }

        oldVideo = new Models.Video
        {
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            Title = title,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            ContentHash = contentHash
        };
        _db.Videos.Add(oldVideo);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} created for file {FileNodeId} by user {UserId}{Canonical}",
            oldVideo.Id, fileNodeId, ownerId,
            userVideo is not null ? $" (canonical={contentHash})" : " (no content hash, old table only)");

        await _eventBus.PublishAsync(new VideoAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            VideoId = oldVideo.Id,
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            FileName = fileName
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "video",
            EntityId = oldVideo.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken);

        return MapToDto(oldVideo, ownerId);
    }

    /// <summary>
    /// Gets a video by ID — queries UserVideo joined with CanonicalVideo.
    /// </summary>
    public async Task<VideoDto?> GetVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo?.CanonicalVideo is not null)
            return MapFromCanonical(userVideo, userVideo.CanonicalVideo);

        // Fallback: old Video table
        var oldVideo = await _db.Videos
            .Include(v => v.Metadata)
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken);

        return oldVideo is null ? null : MapToDto(oldVideo, caller.UserId);
    }

    /// <summary>
    /// Gets a video by its Files-module FileNodeId.
    /// </summary>
    public async Task<VideoDto?> GetVideoByFileNodeIdAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .FirstOrDefaultAsync(uv => uv.FileNodeId == fileNodeId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo?.CanonicalVideo is not null)
            return MapFromCanonical(userVideo, userVideo.CanonicalVideo);

        // Fallback: old Video table
        var oldVideo = await _db.Videos
            .Include(v => v.Metadata)
            .FirstOrDefaultAsync(v => v.FileNodeId == fileNodeId && v.OwnerId == caller.UserId, cancellationToken);

        return oldVideo is null ? null : MapToDto(oldVideo, caller.UserId);
    }

    /// <summary>
    /// Lists videos for the authenticated user. Optionally excludes videos that belong to a series.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> ListVideosAsync(CallerContext caller, int skip = 0, int take = 50, bool excludeSeriesContent = false, CancellationToken cancellationToken = default)
    {
        IQueryable<UserVideo> query = _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted);

        if (excludeSeriesContent)
        {
            var seriesContentHashes = await GetSeriesContentHashesAsync(cancellationToken);
            query = query.Where(uv => !seriesContentHashes.Contains(uv.CanonicalContentHash));
        }

        var userVideos = await query
            .OrderByDescending(uv => uv.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return userVideos
            .Where(uv => uv.CanonicalVideo is not null)
            .Select(uv => MapFromCanonical(uv, uv.CanonicalVideo!))
            .ToList();
    }

    /// <summary>
    /// Searches videos and series by title. Returns series matches + standalone video matches.
    /// </summary>
    public async Task<VideoSearchResultDto> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new VideoSearchResultDto();

        var searchTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLower())
            .ToList();

        // ── Search canonical series ──
        IQueryable<CanonicalVideoSeries> seriesQuery = _db.CanonicalVideoSeries;

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

        // ── Search standalone canonical videos (exclude series-linked) ──
        var seriesContentHashes = await GetSeriesContentHashesAsync(cancellationToken);

        IQueryable<UserVideo> videoQuery = _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && !seriesContentHashes.Contains(uv.CanonicalContentHash));

        foreach (var term in searchTerms)
        {
            var capturedTerm = term;
            videoQuery = videoQuery.Where(uv => uv.CanonicalVideo != null && uv.CanonicalVideo.Title.ToLower().Contains(capturedTerm));
        }

        var userVideos = await videoQuery
            .OrderBy(uv => uv.CanonicalVideo != null ? uv.CanonicalVideo.Title : string.Empty)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return new VideoSearchResultDto
        {
            Series = matchedSeries.Select(s => MapCanonicalSeriesToDto(s)).ToList(),
            StandaloneVideos = userVideos
                .Where(uv => uv.CanonicalVideo is not null)
                .Select(uv => MapFromCanonical(uv, uv.CanonicalVideo!))
                .ToList()
        };
    }

    /// <summary>
    /// Gets recently added videos with paging.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetRecentVideosAsync(CallerContext caller, int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted)
            .OrderByDescending(uv => uv.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return userVideos
            .Where(uv => uv.CanonicalVideo is not null)
            .Select(uv => MapFromCanonical(uv, uv.CanonicalVideo!))
            .ToList();
    }

    /// <summary>
    /// Gets the total video count for a user. Optionally excludes series-linked videos.
    /// </summary>
    public async Task<int> GetVideoCountAsync(Guid ownerId, bool excludeSeriesContent = false, CancellationToken cancellationToken = default)
    {
        IQueryable<UserVideo> query = _db.UserVideos.Where(uv => uv.OwnerId == ownerId && !uv.IsDeleted);

        if (excludeSeriesContent)
        {
            var seriesContentHashes = await GetSeriesContentHashesAsync(cancellationToken);
            query = query.Where(uv => !seriesContentHashes.Contains(uv.CanonicalContentHash));
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets favorite videos, excluding videos that belong to a series.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var seriesContentHashes = await GetSeriesContentHashesAsync(cancellationToken);

        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && uv.IsFavorite && !seriesContentHashes.Contains(uv.CanonicalContentHash))
            .OrderByDescending(uv => uv.UpdatedAt)
            .ToListAsync(cancellationToken);

        return userVideos
            .Where(uv => uv.CanonicalVideo is not null)
            .Select(uv => MapFromCanonical(uv, uv.CanonicalVideo!))
            .ToList();
    }

    /// <summary>
    /// Toggles favorite status on a video.
    /// </summary>
    public async Task<bool> ToggleFavoriteAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try UserVideo first
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo is not null)
        {
            userVideo.IsFavorite = !userVideo.IsFavorite;
            userVideo.UpdatedAt = DateTime.UtcNow;

            // Dual-write: also update old Video record
            var oldVideo = await _db.Videos
                .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken);
            if (oldVideo is not null)
            {
                oldVideo.IsFavorite = userVideo.IsFavorite;
                oldVideo.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Video {VideoId} favorite toggled to {IsFavorite} by user {UserId}",
                videoId, userVideo.IsFavorite, caller.UserId);

            return userVideo.IsFavorite;
        }

        // Fallback: old Video table
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
        // Dual-write: soft-delete UserVideo
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);
        if (userVideo is not null)
        {
            userVideo.IsDeleted = true;
            userVideo.DeletedAt = DateTime.UtcNow;
            userVideo.UpdatedAt = DateTime.UtcNow;
        }

        // Dual-write: soft-delete old Video record
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

    /// <summary>
    /// Maps an old-format Video entity to VideoDto.
    /// </summary>
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
    /// Maps a UserVideo + CanonicalVideo pair to VideoDto.
    /// </summary>
    private VideoDto MapFromCanonical(UserVideo userVideo, CanonicalVideo canonical)
    {
        var watchProgress = _db.WatchProgresses
            .FirstOrDefault(wp => wp.VideoId == userVideo.Id && wp.UserId == userVideo.OwnerId);

        // Check for TMDB enrichment on CanonicalTmdbData
        string? overview = null;
        double? tmdbRating = null;
        string? genres = null;
        DateTime? releaseDate = null;
        bool hasExternalPoster = canonical.HasExternalPoster;

        // Try to load TMDB data linked to old Video record (dual-write fallback)
        var oldVideo = _db.Videos.IgnoreQueryFilters()
            .FirstOrDefault(v => v.FileNodeId == userVideo.FileNodeId && v.OwnerId == userVideo.OwnerId);
        if (oldVideo is not null)
        {
            overview = oldVideo.Overview;
            tmdbRating = oldVideo.TmdbRating;
            genres = oldVideo.Genres;
            releaseDate = oldVideo.ReleaseDate;
            hasExternalPoster = oldVideo.HasExternalPoster || canonical.HasExternalPoster;
        }

        return new VideoDto
        {
            Id = userVideo.Id,
            FileNodeId = userVideo.FileNodeId,
            Title = canonical.Title,
            FileName = canonical.FileName,
            MimeType = canonical.MimeType,
            SizeBytes = canonical.SizeBytes,
            Duration = TimeSpan.FromTicks(canonical.DurationTicks),
            Width = canonical.Metadata?.Width,
            Height = canonical.Metadata?.Height,
            IsFavorite = userVideo.IsFavorite,
            ViewCount = userVideo.ViewCount,
            WatchPositionTicks = watchProgress?.PositionTicks,
            CreatedAt = userVideo.CreatedAt,
            HasExternalPoster = hasExternalPoster,
            Overview = overview,
            TmdbRating = tmdbRating,
            Genres = genres,
            ReleaseDate = releaseDate
        };
    }

    /// <summary>
    /// Returns the set of content hashes that belong to any series (as episodes or franchise items).
    /// </summary>
    private async Task<HashSet<string>> GetSeriesContentHashesAsync(CancellationToken cancellationToken = default)
    {
        var episodeHashes = await _db.CanonicalVideoEpisodes
            .Select(e => e.VideoContentHash)
            .ToListAsync(cancellationToken);

        var franchiseHashes = await _db.CanonicalVideoSeriesItems
            .Select(i => i.VideoContentHash)
            .ToListAsync(cancellationToken);

        return episodeHashes.Concat(franchiseHashes).ToHashSet();
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
    /// Maps a CanonicalVideoSeries entity to a VideoSeriesDto.
    /// </summary>
    private static VideoSeriesDto MapCanonicalSeriesToDto(CanonicalVideoSeries series)
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
            TmdbRating = series.TmdbRating,
            Genres = series.Genres,
            Status = series.Status,
            TotalSeasons = totalSeasons,
            TotalEpisodes = totalEpisodes,
            HasExternalPoster = !string.IsNullOrEmpty(series.PosterHash),
            CreatedAt = series.CreatedAt,
            UpdatedAt = series.UpdatedAt
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

    /// <summary>
    /// Looks up the SHA-256 content hash for a FileNode from the Files module.
    /// Returns null if the FileNode doesn't exist or has no hash.
    /// </summary>
    private async Task<string?> LookupContentHashAsync(Guid fileNodeId, CancellationToken cancellationToken)
    {
        try
        {
            var tableName = _namingStrategy.GetTableName("FileNodes", "core");
            var idCol = _namingStrategy.GetColumnName("Id");
            var hashCol = _namingStrategy.GetColumnName("ContentHash");
            var sql = $"SELECT {hashCol} AS Value FROM {tableName} WHERE {idCol} = {{0}}";
            return await _db.Database
                .SqlQueryRaw<string>(sql, fileNodeId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not look up ContentHash for FileNode {FileNodeId}", fileNodeId);
            return null;
        }
    }
}
