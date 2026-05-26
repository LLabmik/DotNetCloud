using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video series — TV series (with seasons/episodes) and movie franchises.
/// </summary>
public sealed class VideoSeriesService : IVideoSeriesService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoSeriesService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoSeriesService"/> class.
    /// </summary>
    public VideoSeriesService(VideoDbContext db, ILogger<VideoSeriesService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── Series CRUD ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeriesDto> CreateSeriesAsync(CreateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedName = dto.Name.Trim();

        var existing = await _db.VideoSeries
            .AnyAsync(s => s.Name == normalizedName && s.OwnerId == caller.UserId, cancellationToken);
        if (existing)
            throw new BusinessRuleException(ErrorCodes.VideoSeriesAlreadyExists,
                $"A series named '{normalizedName}' already exists.");

        var series = new VideoSeries
        {
            OwnerId = caller.UserId,
            Name = normalizedName,
            Description = dto.Description,
            Type = ParseSeriesType(dto.Type),
            Year = dto.Year
        };

        _db.VideoSeries.Add(series);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Series {SeriesId} '{Name}' ({Type}) created by user {UserId}",
            series.Id, series.Name, series.Type, caller.UserId);

        return MapToDto(series);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto?> GetSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        return series is null ? null : MapToDto(series);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesDto>> ListSeriesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .Where(s => s.OwnerId == caller.UserId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return series.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> UpdateSeriesAsync(Guid seriesId, UpdateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        if (dto.Name is not null)
        {
            var normalizedName = dto.Name.Trim();
            var duplicate = await _db.VideoSeries
                .AnyAsync(s => s.Name == normalizedName && s.Id != seriesId && s.OwnerId == caller.UserId, cancellationToken);
            if (duplicate)
                throw new BusinessRuleException(ErrorCodes.VideoSeriesAlreadyExists,
                    $"A series named '{normalizedName}' already exists.");
            series.Name = normalizedName;
        }

        if (dto.Description is not null)
            series.Description = dto.Description;
        if (dto.Type is not null)
            series.Type = ParseSeriesType(dto.Type);
        if (dto.Year.HasValue)
            series.Year = dto.Year;

        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(series);
    }

    /// <inheritdoc />
    public async Task DeleteSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        series.IsDeleted = true;
        series.DeletedAt = DateTime.UtcNow;
        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Series {SeriesId} '{Name}' soft-deleted by user {UserId}",
            seriesId, series.Name, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> FindOrCreateByNameAsync(string name, string type, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        var seriesType = ParseSeriesType(type);

        var existing = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Name == normalizedName && s.OwnerId == caller.UserId && !s.IsDeleted, cancellationToken);

        if (existing is not null)
            return MapToDto(existing);

        var series = new VideoSeries
        {
            OwnerId = caller.UserId,
            Name = normalizedName,
            Type = seriesType
        };

        _db.VideoSeries.Add(series);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Series {SeriesId} '{Name}' auto-created by FindOrCreateByName for user {UserId}",
            series.Id, series.Name, caller.UserId);

        return MapToDto(series);
    }

    // ─── Franchise Items ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeriesItemDto> AddVideoToSeriesAsync(Guid seriesId, Guid videoId, int? sortOrder, string? episodeTitle, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var videoExists = await _db.Videos.AnyAsync(v => v.Id == videoId, cancellationToken);
        if (!videoExists)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInSeries = await _db.VideoSeriesItems
            .AnyAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken);
        if (alreadyInSeries)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeries, "Video is already in this series.");

        var maxOrder = sortOrder ?? (await _db.VideoSeriesItems
            .Where(i => i.SeriesId == seriesId)
            .MaxAsync(i => (int?)i.SortOrder, cancellationToken) ?? -1) + 1;

        var item = new VideoSeriesItem
        {
            SeriesId = seriesId,
            VideoId = videoId,
            SortOrder = maxOrder,
            EpisodeTitle = episodeTitle
        };

        _db.VideoSeriesItems.Add(item);
        series.UpdatedAt = DateTime.UtcNow;

        if (sortOrder.HasValue)
        {
            // Reorder existing items to make room
            var existingItems = await _db.VideoSeriesItems
                .Where(i => i.SeriesId == seriesId && i.Id != item.Id && i.SortOrder >= sortOrder.Value)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingItems)
                existing.SortOrder++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} added to series {SeriesId} at sort order {SortOrder}",
            videoId, seriesId, maxOrder);

        return new VideoSeriesItemDto
        {
            Id = item.Id,
            SeriesId = item.SeriesId,
            VideoId = item.VideoId,
            SortOrder = item.SortOrder,
            EpisodeTitle = item.EpisodeTitle
        };
    }

    /// <inheritdoc />
    public async Task RemoveVideoFromSeriesAsync(Guid seriesId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var item = await _db.VideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        _db.VideoSeriesItems.Remove(item);
        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReorderSeriesItemAsync(Guid seriesId, Guid videoId, int newSortOrder, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var item = await _db.VideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        var oldOrder = item.SortOrder;
        item.SortOrder = newSortOrder;
        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} reordered in series {SeriesId}: {OldOrder} -> {NewOrder}",
            videoId, seriesId, oldOrder, newSortOrder);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesItemDto>> GetSeriesVideosAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        if (series is null)
            return [];

        var items = await _db.VideoSeriesItems
            .Include(i => i.Video).ThenInclude(v => v!.Metadata)
            .Where(i => i.SeriesId == seriesId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        return items.Select(i => new VideoSeriesItemDto
        {
            Id = i.Id,
            SeriesId = i.SeriesId,
            VideoId = i.VideoId,
            SortOrder = i.SortOrder,
            EpisodeTitle = i.EpisodeTitle,
            Video = MapVideoToDto(i.Video!, caller.UserId)
        }).ToList();
    }

    // ─── Seasons ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeasonDto> CreateSeasonAsync(CreateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == dto.SeriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var duplicateSeason = await _db.VideoSeasons
            .AnyAsync(s => s.SeriesId == dto.SeriesId && s.SeasonNumber == dto.SeasonNumber, cancellationToken);
        if (duplicateSeason)
            throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound,
                $"Season {dto.SeasonNumber} already exists in this series.");

        var season = new VideoSeason
        {
            SeriesId = dto.SeriesId,
            SeasonNumber = dto.SeasonNumber,
            Name = dto.Name,
            Overview = dto.Overview
        };

        _db.VideoSeasons.Add(season);
        series.TotalSeasons = await _db.VideoSeasons.CountAsync(s => s.SeriesId == dto.SeriesId, cancellationToken);
        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Season {SeasonNumber} created in series {SeriesId}",
            dto.SeasonNumber, dto.SeriesId);

        return MapSeasonToDto(season);
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto?> GetSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken);

        return season is null ? null : MapSeasonToDto(season);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeasonDto>> GetSeriesSeasonsAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        if (series is null)
            return [];

        var seasons = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .Where(s => s.SeriesId == seriesId)
            .OrderBy(s => s.SeasonNumber)
            .ToListAsync(cancellationToken);

        return seasons.Select(MapSeasonToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto> UpdateSeasonAsync(Guid seasonId, UpdateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        if (dto.SeasonNumber.HasValue)
            season.SeasonNumber = dto.SeasonNumber.Value;
        if (dto.Name is not null)
            season.Name = dto.Name;
        if (dto.Overview is not null)
            season.Overview = dto.Overview;

        season.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapSeasonToDto(season);
    }

    /// <inheritdoc />
    public async Task DeleteSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        season.IsDeleted = true;
        season.DeletedAt = DateTime.UtcNow;
        season.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Update series total seasons count
        var series = await _db.VideoSeries.FindAsync(new object[] { season.SeriesId }, cancellationToken);
        if (series is not null)
        {
            series.TotalSeasons = await _db.VideoSeasons.CountAsync(s => s.SeriesId == season.SeriesId, cancellationToken);
            series.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Season {SeasonId} in series {SeriesId} soft-deleted", seasonId, season.SeriesId);
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto> FindOrCreateSeasonAsync(Guid seriesId, int seasonNumber, string? name, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.SeriesId == seriesId && s.SeasonNumber == seasonNumber, cancellationToken);

        if (season is not null)
            return MapSeasonToDto(season);

        season = new VideoSeason
        {
            SeriesId = seriesId,
            SeasonNumber = seasonNumber,
            Name = name ?? $"Season {seasonNumber}"
        };

        _db.VideoSeasons.Add(season);
        await _db.SaveChangesAsync(cancellationToken);

        return MapSeasonToDto(season);
    }

    // ─── Episodes ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoEpisodeDto> AddEpisodeAsync(Guid seasonId, Guid videoId, int episodeNumber, string? title, string? overview, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .Include(s => s.Series)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        var videoExists = await _db.Videos.AnyAsync(v => v.Id == videoId, cancellationToken);
        if (!videoExists)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInSeason = await _db.VideoEpisodes
            .AnyAsync(e => e.SeasonId == seasonId && e.VideoId == videoId, cancellationToken);
        if (alreadyInSeason)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeason, "Video is already an episode in this season.");

        var maxOrder = await _db.VideoEpisodes
            .Where(e => e.SeasonId == seasonId)
            .MaxAsync(e => (int?)e.SortOrder, cancellationToken) ?? 0;

        var episode = new VideoEpisode
        {
            SeasonId = seasonId,
            VideoId = videoId,
            EpisodeNumber = episodeNumber,
            Title = title,
            Overview = overview,
            SortOrder = maxOrder + 1
        };

        _db.VideoEpisodes.Add(episode);
        season.EpisodeCount = await _db.VideoEpisodes.CountAsync(e => e.SeasonId == seasonId, cancellationToken);
        season.UpdatedAt = DateTime.UtcNow;

        // Update series totals
        var series = season.Series!;
        series.TotalEpisodes = await _db.VideoEpisodes
            .CountAsync(e => e.Season!.SeriesId == series.Id, cancellationToken);
        series.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} added as episode {EpisodeNumber} to season {SeasonId}",
            videoId, episodeNumber, seasonId);

        return new VideoEpisodeDto
        {
            Id = episode.Id,
            SeasonId = episode.SeasonId,
            VideoId = episode.VideoId,
            EpisodeNumber = episode.EpisodeNumber,
            Title = episode.Title,
            Overview = episode.Overview,
            SortOrder = episode.SortOrder
        };
    }

    /// <inheritdoc />
    public async Task RemoveEpisodeAsync(Guid seasonId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .Include(s => s.Series)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        var episode = await _db.VideoEpisodes
            .FirstOrDefaultAsync(e => e.SeasonId == seasonId && e.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoEpisodeNotFound, "Episode not found.");

        _db.VideoEpisodes.Remove(episode);

        season.EpisodeCount = await _db.VideoEpisodes.CountAsync(e => e.SeasonId == seasonId, cancellationToken);
        season.UpdatedAt = DateTime.UtcNow;

        var series = season.Series!;
        series.TotalEpisodes = await _db.VideoEpisodes
            .CountAsync(e => e.Season!.SeriesId == series.Id, cancellationToken);
        series.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoEpisodeDto>> GetSeasonEpisodesAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var season = await _db.VideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken);

        if (season is null)
            return [];

        var episodes = await _db.VideoEpisodes
            .Include(e => e.Video).ThenInclude(v => v!.Metadata)
            .Where(e => e.SeasonId == seasonId)
            .OrderBy(e => e.EpisodeNumber)
            .ThenBy(e => e.SortOrder)
            .ToListAsync(cancellationToken);

        return episodes.Select(e => new VideoEpisodeDto
        {
            Id = e.Id,
            SeasonId = e.SeasonId,
            VideoId = e.VideoId,
            EpisodeNumber = e.EpisodeNumber,
            Title = e.Title,
            Overview = e.Overview,
            SortOrder = e.SortOrder,
            Video = MapVideoToDto(e.Video!, caller.UserId)
        }).ToList();
    }

    // ─── Thumbnail ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]?> GetSeriesThumbnailAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .Where(s => s.Id == seriesId && s.OwnerId == caller.UserId && !s.IsDeleted)
            .Select(s => s.ThumbnailPoster)
            .FirstOrDefaultAsync(cancellationToken);

        return series;
    }

    // ─── Auto-Detection ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesDto>> DetectSeriesFromLibraryAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Where(v => v.OwnerId == caller.UserId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        // Group by potential series names extracted from folder-like patterns in filenames
        var seriesGroups = new Dictionary<string, (List<Guid> VideoIds, SeriesType Type)>();

        foreach (var video in videos)
        {
            var (seriesName, seriesType) = DetectSeriesFromVideo(video.FileName, video.Title);
            if (seriesName is null)
                continue;

            if (!seriesGroups.ContainsKey(seriesName))
                seriesGroups[seriesName] = (new List<Guid>(), seriesType);

            seriesGroups[seriesName].VideoIds.Add(video.Id);
        }

        var results = new List<VideoSeriesDto>();
        foreach (var (name, (videoIds, type)) in seriesGroups.OrderBy(g => g.Key))
        {
            if (videoIds.Count < 2)
                continue; // Only group if 2+ videos

            var series = await FindOrCreateByNameAsync(name, type.ToString(), caller, cancellationToken);
            results.Add(series);
        }

        return results;
    }

    /// <summary>
    /// Attempts to detect a series name and type from a filename.
    /// Looks for patterns like "Series.Name.S01E01" or folder-name patterns.
    /// </summary>
    private static (string? SeriesName, SeriesType Type) DetectSeriesFromVideo(string fileName, string title)
    {
        // Pattern: "Series.Name.S01E01.ext" or "Series.Name.S1E1.ext"
        var tvMatch = System.Text.RegularExpressions.Regex.Match(fileName,
            @"^(.+?)[._\s]+[Ss](\d{1,2})[Ee](\d{1,3})",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        if (tvMatch.Success)
        {
            var seriesName = tvMatch.Groups[1].Value
                .Replace('.', ' ')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();
            // Clean up extra whitespace
            seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s+", " ");
            return (seriesName, SeriesType.TvSeries);
        }

        // Pattern: "Series.Name.Season.01.Episode.01.ext"
        var tvMatch2 = System.Text.RegularExpressions.Regex.Match(fileName,
            @"^(.+?)[._\s]+[Ss]eason[.\s]*\d+",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        if (tvMatch2.Success)
        {
            var seriesName = tvMatch2.Groups[1].Value
                .Replace('.', ' ')
                .Replace('_', ' ')
                .Trim();
            seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s+", " ");
            return (seriesName, SeriesType.TvSeries);
        }

        // For movies that are in year format: "Movie Title (1999).ext" or "Movie Title 1999.ext"
        // We don't auto-detect movie franchises from single files — that requires TMDB collection data
        return (null, SeriesType.MovieFranchise);
    }

    // ─── Mapping Helpers ─────────────────────────────────────────────

    private VideoSeriesDto MapToDto(VideoSeries series)
    {
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
            TotalSeasons = series.Seasons?.Count ?? 0,
            TotalEpisodes = series.TotalEpisodes,
            HasExternalPoster = series.HasExternalPoster,
            CreatedAt = series.CreatedAt,
            UpdatedAt = series.UpdatedAt
        };
    }

    private static VideoSeasonDto MapSeasonToDto(VideoSeason season)
    {
        return new VideoSeasonDto
        {
            Id = season.Id,
            SeriesId = season.SeriesId,
            SeasonNumber = season.SeasonNumber,
            Name = season.Name,
            Overview = season.Overview,
            EpisodeCount = season.Episodes?.Count ?? 0,
            HasExternalPoster = season.HasExternalPoster,
            AirDate = season.AirDate,
            CreatedAt = season.CreatedAt
        };
    }

    private VideoDto? MapVideoToDto(Models.Video? video, Guid userId)
    {
        if (video is null)
            return null;

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
    /// Parses a series type string to enum. Defaults to TvSeries.
    /// </summary>
    private static SeriesType ParseSeriesType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return SeriesType.TvSeries;

        return type.Trim().Equals("MovieFranchise", StringComparison.OrdinalIgnoreCase)
            ? SeriesType.MovieFranchise
            : SeriesType.TvSeries;
    }
}
