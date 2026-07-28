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
using static DotNetCloud.Modules.Video.Data.Services.WatchProgressService;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing videos — CRUD, search, recently watched, favorites.
/// Uses canonical/user-junction data model for content deduplication.
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly VideoDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IVideoSeriesService _seriesService;
    private readonly ITableNamingStrategy _namingStrategy;
    private readonly ILogger<VideoService> _logger;

    // Per-circuit cache: series content hashes rarely change (only on library scan).
    // Avoids scanning CanonicalVideoEpisodes + CanonicalVideoSeriesItems on every page turn.
    private HashSet<string>? _cachedSeriesContentHashes;

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
    /// Uses canonical/user-junction model for content deduplication.
    /// </summary>
    public async Task<VideoDto> CreateVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // ── Duplicate check: return existing UserVideo if same FileNodeId + OwnerId ──
        var existing = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .FirstOrDefaultAsync(uv => uv.FileNodeId == fileNodeId && uv.OwnerId == ownerId && !uv.IsDeleted, cancellationToken);
        if (existing?.CanonicalVideo is not null)
            return MapFromCanonical(existing, existing.CanonicalVideo);

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

        // ── User-video junction ──
        // Use fileNodeId as fallback content hash when FileNode record isn't available yet.
        contentHash ??= fileNodeId.ToString("N");

        // Ensure a CanonicalVideo exists for the (possibly fallback) content hash
        if (canonical is null)
        {
            canonical = await _db.CanonicalVideos
                .FirstOrDefaultAsync(cv => cv.ContentHash == contentHash, cancellationToken);

            if (canonical is null)
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
                _logger.LogInformation("CanonicalVideo {ContentHash} fallback-created for file {FileNodeId}", contentHash, fileNodeId);
            }
        }

        var userVideo = new UserVideo
        {
            OwnerId = ownerId,
            FileNodeId = fileNodeId,
            CanonicalContentHash = contentHash
        };
        _db.UserVideos.Add(userVideo);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} created for file {FileNodeId} by user {UserId} (canonical={ContentHash})",
            userVideo.Id, fileNodeId, ownerId, contentHash);

        await _eventBus.PublishAsync(new VideoAddedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            VideoId = userVideo.Id,
            FileNodeId = fileNodeId,
            OwnerId = ownerId,
            FileName = fileName
        }, caller, cancellationToken);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "video",
            EntityId = userVideo.Id.ToString(),
            Action = SearchIndexAction.Index
        }, caller, cancellationToken);

        return MapFromCanonical(userVideo, canonical!);
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

        return null;
    }

    /// <summary>
    /// Backfills DurationTicks on the canonical video if currently 0.
    /// Called from the streaming pipeline when ffprobe has already extracted the duration.
    /// </summary>
    public async Task UpdateDurationAsync(Guid videoId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero) return;

        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId, cancellationToken);

        if (userVideo?.CanonicalVideo is { DurationTicks: 0 })
        {
            userVideo.CanonicalVideo.DurationTicks = duration.Ticks;
            userVideo.CanonicalVideo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
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

        return null;
    }

    /// <summary>
    /// Lists videos for the authenticated user. Optionally excludes videos that belong to a series.
    /// When <paramref name="sortAlphabetically"/> is true, sorts by title A-Z instead of newest first.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> ListVideosAsync(CallerContext caller, int skip = 0, int take = 50, bool excludeSeriesContent = false, bool sortAlphabetically = false, CancellationToken cancellationToken = default)
    {
        IQueryable<UserVideo> query = _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted);

        if (excludeSeriesContent)
        {
            // NOT EXISTS subqueries are far faster than loading all hashes into memory with NOT IN.
            // The database uses indexes on VideoContentHash and short-circuits on first match.
            query = query.Where(uv => !_db.CanonicalVideoEpisodes.Any(e => e.VideoContentHash == uv.CanonicalContentHash)
                                   && !_db.CanonicalVideoSeriesItems.Any(i => i.VideoContentHash == uv.CanonicalContentHash));
        }

        IQueryable<UserVideo> orderedQuery;
        if (sortAlphabetically)
        {
            orderedQuery = query
                .Where(uv => uv.CanonicalVideo != null)
                .OrderBy(uv => uv.CanonicalVideo!.Title);
        }
        else
        {
            orderedQuery = query.OrderByDescending(uv => uv.CreatedAt);
        }

        var userVideos = await orderedQuery
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return userVideos
            .Where(uv => uv.CanonicalVideo is not null)
            .Select(uv => MapFromCanonical(uv, uv.CanonicalVideo!))
            .ToList();
    }

    /// <summary>
    /// Lists all videos across all users — for search indexing only.
    /// Does not filter by OwnerId; each result includes the owning user's ID.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> ListAllVideosAsync(int skip = 0, int take = int.MaxValue, CancellationToken cancellationToken = default)
    {
        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => !uv.IsDeleted)
            .OrderBy(uv => uv.CreatedAt)
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
        IQueryable<UserVideo> videoQuery = _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted
                && !_db.CanonicalVideoEpisodes.Any(e => e.VideoContentHash == uv.CanonicalContentHash)
                && !_db.CanonicalVideoSeriesItems.Any(i => i.VideoContentHash == uv.CanonicalContentHash));

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
            query = query.Where(uv => !_db.CanonicalVideoEpisodes.Any(e => e.VideoContentHash == uv.CanonicalContentHash)
                                   && !_db.CanonicalVideoSeriesItems.Any(i => i.VideoContentHash == uv.CanonicalContentHash));
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Gets favorite videos, excluding videos that belong to a series.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetFavoritesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && uv.IsFavorite
                && !_db.CanonicalVideoEpisodes.Any(e => e.VideoContentHash == uv.CanonicalContentHash)
                && !_db.CanonicalVideoSeriesItems.Any(i => i.VideoContentHash == uv.CanonicalContentHash))
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
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        userVideo.IsFavorite = !userVideo.IsFavorite;
        userVideo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} favorite toggled to {IsFavorite} by user {UserId}",
            videoId, userVideo.IsFavorite, caller.UserId);

        return userVideo.IsFavorite;
    }

    /// <summary>
    /// Soft-deletes a video.
    /// </summary>
    public async Task DeleteVideoAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        userVideo.IsDeleted = true;
        userVideo.DeletedAt = DateTime.UtcNow;
        userVideo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} soft-deleted by user {UserId}", videoId, caller.UserId);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "video",
            EntityId = videoId.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task IncrementViewCountAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId, cancellationToken);

        if (userVideo is not null && !userVideo.IsDeleted)
        {
            userVideo.ViewCount++;
            userVideo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public void InvalidateLibraryCache()
    {
        _cachedSeriesContentHashes = null;
    }

    /// <summary>
    /// Maps a UserVideo + CanonicalVideo pair to VideoDto.
    /// </summary>
    private VideoDto MapFromCanonical(UserVideo userVideo, CanonicalVideo canonical)
    {
        // Check for TMDB enrichment on CanonicalTmdbData
        string? overview = null;
        double? tmdbRating = null;
        string? genres = null;
        DateTime? releaseDate = null;
        bool hasExternalPoster = canonical.HasExternalPoster;
        string? tmdbTagline = null;
        int? tmdbVoteCount = null;
        string? tmdbOriginalLanguage = null;
        string? tmdbOriginalTitle = null;

        // Load TMDB data from canonical enrichment — check both enrichment TmdbId and embedded TmdbId
        var tmdbId = canonical.TmdbId ?? canonical.EmbeddedTmdbId;
        if (tmdbId is not null)
        {
            var tmdbData = _db.CanonicalTmdbData
                .FirstOrDefault(ct => ct.TmdbId == tmdbId.Value);
            if (tmdbData is not null)
            {
                overview = tmdbData.Overview;
                tmdbRating = tmdbData.TmdbRating;
                genres = tmdbData.Genres;
                releaseDate = tmdbData.ReleaseDate;
                hasExternalPoster = tmdbData.ExternalPosterHash is not null || canonical.HasExternalPoster;
                tmdbTagline = tmdbData.Tagline;
                tmdbVoteCount = tmdbData.VoteCount;
                tmdbOriginalLanguage = tmdbData.OriginalLanguage;
                tmdbOriginalTitle = tmdbData.OriginalTitle;
            }
        }

        var hasThumbnail = canonical.ThumbnailPosterHash is not null || canonical.ExternalPosterHash is not null;

        return new VideoDto
        {
            Id = userVideo.Id,
            OwnerId = userVideo.OwnerId,
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
            WatchPositionTicks = WatchProgressService.ApplyResumeLogic(userVideo.WatchPositionTicks, canonical.DurationTicks),
            CreatedAt = userVideo.CreatedAt,
            HasExternalPoster = hasExternalPoster,
            HasThumbnail = hasThumbnail,
            Overview = overview,
            TmdbRating = tmdbRating,
            Genres = genres,
            ReleaseDate = releaseDate,
            TmdbTagline = tmdbTagline,
            TmdbVoteCount = tmdbVoteCount,
            TmdbOriginalLanguage = tmdbOriginalLanguage,
            TmdbOriginalTitle = tmdbOriginalTitle
        };
    }

    /// <summary>
    /// Returns the set of content hashes that belong to any series (as episodes or franchise items).
    /// </summary>
    private async Task<HashSet<string>> GetSeriesContentHashesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSeriesContentHashes is not null)
            return _cachedSeriesContentHashes;

        var episodeHashes = await _db.CanonicalVideoEpisodes
            .Select(e => e.VideoContentHash)
            .ToListAsync(cancellationToken);

        var franchiseHashes = await _db.CanonicalVideoSeriesItems
            .Select(i => i.VideoContentHash)
            .ToListAsync(cancellationToken);

        _cachedSeriesContentHashes = episodeHashes.Concat(franchiseHashes).ToHashSet();
        return _cachedSeriesContentHashes;
    }

    /// <summary>
    /// Returns combined library content with two-phase server-side paging.
    /// Series slots are consumed first (sorted by name), then standalone video slots (sorted by title, A-Z).
    /// This avoids loading all data into memory — only the current page is fetched.
    /// When <paramref name="preloadedSeries"/> is provided, skips the expensive ListSeriesAsync call
    /// (series data doesn't change between page views).
    /// </summary>
    public async Task<VideoLibraryContentDto> ListLibraryContentAsync(CallerContext caller, int skip = 0, int take = 50, IReadOnlyList<VideoSeriesDto>? preloadedSeries = null, CancellationToken cancellationToken = default)
    {
        var allSeries = preloadedSeries ?? await _seriesService.ListSeriesAsync(caller, cancellationToken);
        var seriesCount = allSeries.Count;
        var totalStandalone = await GetVideoCountAsync(caller.UserId, excludeSeriesContent: true, cancellationToken);

        IReadOnlyList<VideoSeriesDto> pageSeries;
        IReadOnlyList<VideoDto> pageVideos;

        if (skip < seriesCount)
        {
            // Page starts in the series range
            var seriesOnPage = Math.Min(take, seriesCount - skip);
            pageSeries = allSeries.Skip(skip).Take(seriesOnPage).ToList();

            var videoTake = take - seriesOnPage;
            pageVideos = videoTake > 0
                ? await ListVideosAsync(caller, 0, videoTake, excludeSeriesContent: true, sortAlphabetically: true, cancellationToken)
                : [];
        }
        else
        {
            // Page starts past all series — entirely in video range
            pageSeries = [];
            var videoSkip = skip - seriesCount;
            pageVideos = await ListVideosAsync(caller, videoSkip, take, excludeSeriesContent: true, sortAlphabetically: true, cancellationToken);
        }

        return new VideoLibraryContentDto
        {
            Series = pageSeries,
            StandaloneVideos = pageVideos,
            TotalSeries = seriesCount,
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
