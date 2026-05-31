using System.IO;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video series — TV series (with seasons/episodes) and movie franchises.
/// Uses canonical (shared) tables for content deduplication.
/// </summary>
public sealed class VideoSeriesService : IVideoSeriesService
{
    private readonly VideoDbContext _db;
    private readonly ITmdbClient _tmdbClient;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly ILogger<VideoSeriesService> _logger;
    private readonly string _storageRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoSeriesService"/> class.
    /// </summary>
    public VideoSeriesService(VideoDbContext db, ITmdbClient tmdbClient, ILogger<VideoSeriesService> logger, IConfiguration configuration)
    {
        _db = db;
        _tmdbClient = tmdbClient;
        _logger = logger;
        _storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _contentStorage = new ContentAddressedStorage(_storageRoot);
    }

    // ─── Series CRUD ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeriesDto> CreateSeriesAsync(CreateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedName = dto.Name.Trim();

        // Check canonical series by name (shared across users)
        var existingCanonical = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Name == normalizedName, cancellationToken);

        if (existingCanonical is not null)
            return MapCanonicalToDto(existingCanonical);

        // ── Create canonical series (shared) ──
        var canonicalSeries = new CanonicalVideoSeries
        {
            Name = normalizedName,
            Description = dto.Description,
            Type = ParseSeriesType(dto.Type)
        };
        _db.CanonicalVideoSeries.Add(canonicalSeries);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeries {SeriesId} '{Name}' ({Type}) created",
            canonicalSeries.Id, normalizedName, canonicalSeries.Type);

        return MapCanonicalToDto(canonicalSeries);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto?> GetSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        return canonical is not null ? MapCanonicalToDto(canonical) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesDto>> ListSeriesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Get content hashes for videos owned by this user
        var userContentHashes = await _db.UserVideos
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted)
            .Select(uv => uv.CanonicalContentHash)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userContentHashes.Count == 0)
            return [];

        // Find series that have episodes or franchise items matching the user's videos
        var episodeSeriesIds = await _db.CanonicalVideoEpisodes
            .Where(e => userContentHashes.Contains(e.VideoContentHash))
            .Select(e => e.SeasonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var seasonSeriesIds = await _db.CanonicalVideoSeasons
            .Where(s => episodeSeriesIds.Contains(s.Id))
            .Select(s => s.SeriesId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var franchiseSeriesIds = await _db.CanonicalVideoSeriesItems
            .Where(i => userContentHashes.Contains(i.VideoContentHash))
            .Select(i => i.SeriesId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var matchingSeriesIds = seasonSeriesIds.Concat(franchiseSeriesIds).Distinct().ToHashSet();

        if (matchingSeriesIds.Count == 0)
            return [];

        var canonicalSeries = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .Where(s => matchingSeriesIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return canonicalSeries
            .Where(s => s.TotalEpisodes > 1 || (s.Items?.Count ?? 0) > 1)
            .Select(MapCanonicalToDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> UpdateSeriesAsync(Guid seriesId, UpdateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        if (dto.Name is not null)
        {
            var normalizedName = dto.Name.Trim();
            var duplicate = await _db.CanonicalVideoSeries
                .AnyAsync(s => s.Name == normalizedName && s.Id != seriesId, cancellationToken);
            if (duplicate)
                throw new BusinessRuleException(ErrorCodes.VideoSeriesAlreadyExists,
                    $"A series named '{normalizedName}' already exists.");
            canonical.Name = normalizedName;
        }

        if (dto.Description is not null)
            canonical.Description = dto.Description;
        if (dto.Type is not null)
            canonical.Type = ParseSeriesType(dto.Type);

        canonical.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapCanonicalToDto(canonical);
    }

    /// <inheritdoc />
    public async Task DeleteSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        // Remove related child records
        var seasons = await _db.CanonicalVideoSeasons
            .Where(s => s.SeriesId == seriesId).ToListAsync(cancellationToken);
        _db.CanonicalVideoSeasons.RemoveRange(seasons);

        var items = await _db.CanonicalVideoSeriesItems
            .Where(i => i.SeriesId == seriesId).ToListAsync(cancellationToken);
        _db.CanonicalVideoSeriesItems.RemoveRange(items);

        _db.CanonicalVideoSeries.Remove(canonical);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeries {SeriesId} deleted", seriesId);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> FindOrCreateByNameAsync(string name, string type, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        var seriesType = ParseSeriesType(type);

        var existingCanonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Name == normalizedName, cancellationToken);

        if (existingCanonical is not null)
            return MapCanonicalToDto(existingCanonical);

        // ── Create canonical series ──
        var canonical = new CanonicalVideoSeries
        {
            Name = normalizedName,
            Type = seriesType
        };
        _db.CanonicalVideoSeries.Add(canonical);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeries {SeriesId} '{Name}' auto-created", canonical.Id, normalizedName);

        return MapCanonicalToDto(canonical);
    }

    // ─── Franchise Items ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeriesItemDto> AddVideoToSeriesAsync(Guid seriesId, Guid videoId, int? sortOrder, string? episodeTitle, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve video's content hash
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(contentHash))
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found (no content hash).");

        // Check canonical series
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var alreadyInSeries = await _db.CanonicalVideoSeriesItems
            .AnyAsync(i => i.SeriesId == seriesId && i.VideoContentHash == contentHash, cancellationToken);
        if (alreadyInSeries)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeries, "Video is already in this series.");

        var maxOrder = sortOrder ?? (await _db.CanonicalVideoSeriesItems
            .Where(i => i.SeriesId == seriesId)
            .MaxAsync(i => (int?)i.SortOrder, cancellationToken) ?? -1) + 1;

        var item = new CanonicalVideoSeriesItem
        {
            SeriesId = seriesId,
            VideoContentHash = contentHash,
            SortOrder = maxOrder,
            EpisodeTitle = episodeTitle
        };

        _db.CanonicalVideoSeriesItems.Add(item);
        canonicalSeries.UpdatedAt = DateTime.UtcNow;

        if (sortOrder.HasValue)
        {
            var existingItems = await _db.CanonicalVideoSeriesItems
                .Where(i => i.SeriesId == seriesId && i.Id != item.Id && i.SortOrder >= sortOrder.Value)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingItems)
                existing.SortOrder++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} (hash={ContentHash}) added to canonical series {SeriesId} at sort order {SortOrder}",
            videoId, contentHash, seriesId, maxOrder);

        return new VideoSeriesItemDto
        {
            Id = item.Id,
            SeriesId = item.SeriesId,
            VideoId = videoId,
            SortOrder = item.SortOrder,
            EpisodeTitle = item.EpisodeTitle
        };
    }

    /// <inheritdoc />
    public async Task RemoveVideoFromSeriesAsync(Guid seriesId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(contentHash))
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var item = await _db.CanonicalVideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoContentHash == contentHash, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        _db.CanonicalVideoSeriesItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReorderSeriesItemAsync(Guid seriesId, Guid videoId, int newSortOrder, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(contentHash))
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var canonicalItem = await _db.CanonicalVideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoContentHash == contentHash, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        var oldOrder = canonicalItem.SortOrder;
        canonicalItem.SortOrder = newSortOrder;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} reordered in series {SeriesId}: {OldOrder} -> {NewOrder}",
            videoId, seriesId, oldOrder, newSortOrder);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesItemDto>> GetSeriesVideosAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonicalSeries is null)
            return [];

        var items = await _db.CanonicalVideoSeriesItems
            .Where(i => i.SeriesId == seriesId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return [];

        // Look up UserVideos for this user by content hash to include
        // the actual video details (Id, FileNodeId, etc.) so the player
        // can stream the content when a series item is clicked.
        var contentHashes = items.Select(i => i.VideoContentHash).Distinct().ToList();
        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && contentHashes.Contains(uv.CanonicalContentHash))
            .ToListAsync(cancellationToken);

        var videoByHash = userVideos
            .Where(uv => uv.CanonicalVideo is not null)
            .ToDictionary(uv => uv.CanonicalContentHash, uv => MapUserVideoToDto(uv));

        return items.Select(i => new VideoSeriesItemDto
        {
            Id = i.Id,
            SeriesId = i.SeriesId,
            VideoId = videoByHash.TryGetValue(i.VideoContentHash, out var videoDto) ? videoDto.Id : Guid.Empty,
            SortOrder = i.SortOrder,
            EpisodeTitle = i.EpisodeTitle ?? videoDto?.Title,
            Video = videoDto
        }).ToList();
    }

    private static VideoDto MapUserVideoToDto(UserVideo uv)
    {
        var canonical = uv.CanonicalVideo!;
        return new VideoDto
        {
            Id = uv.Id,
            FileNodeId = uv.FileNodeId,
            Title = canonical.Title,
            FileName = canonical.FileName,
            MimeType = canonical.MimeType,
            SizeBytes = canonical.SizeBytes,
            Duration = TimeSpan.FromTicks(canonical.DurationTicks),
            Width = canonical.Metadata?.Width,
            Height = canonical.Metadata?.Height,
            IsFavorite = uv.IsFavorite,
            ViewCount = uv.ViewCount,
            CreatedAt = uv.CreatedAt,
            HasExternalPoster = canonical.HasExternalPoster,
        };
    }

    // ─── Seasons ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoSeasonDto> CreateSeasonAsync(CreateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == dto.SeriesId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var duplicateSeason = await _db.CanonicalVideoSeasons
            .AnyAsync(s => s.SeriesId == dto.SeriesId && s.SeasonNumber == dto.SeasonNumber, cancellationToken);
        if (duplicateSeason)
            throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound,
                $"Season {dto.SeasonNumber} already exists in this series.");

        var season = new CanonicalVideoSeason
        {
            SeriesId = dto.SeriesId,
            SeasonNumber = dto.SeasonNumber,
            Name = dto.Name,
            Overview = dto.Overview
        };

        _db.CanonicalVideoSeasons.Add(season);
        canonicalSeries.TotalSeasons = await _db.CanonicalVideoSeasons
            .CountAsync(s => s.SeriesId == dto.SeriesId, cancellationToken);
        canonicalSeries.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeason {SeasonNumber} created in series {SeriesId}",
            dto.SeasonNumber, dto.SeriesId);

        return MapCanonicalSeasonToDto(season);
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto?> GetSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonical = await _db.CanonicalVideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (canonical is not null)
            return MapCanonicalSeasonToDto(canonical);

        // Fallback: old per-user
        var old = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken);

        return old is null ? null : MapSeasonToDto(old);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeasonDto>> GetSeriesSeasonsAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonicalSeries is not null)
        {
            var seasons = await _db.CanonicalVideoSeasons
                .Include(s => s.Episodes)
                .Where(s => s.SeriesId == seriesId)
                .OrderBy(s => s.SeasonNumber)
                .ToListAsync(cancellationToken);

            return seasons.Select(MapCanonicalSeasonToDto).ToList();
        }

        // Fallback: old per-user
        var oldSeries = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        if (oldSeries is null)
            return [];

        var oldSeasons = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .Where(s => s.SeriesId == seriesId)
            .OrderBy(s => s.SeasonNumber)
            .ToListAsync(cancellationToken);

        return oldSeasons.Select(MapSeasonToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto> UpdateSeasonAsync(Guid seasonId, UpdateVideoSeasonDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonical = await _db.CanonicalVideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (canonical is not null)
        {
            if (dto.SeasonNumber.HasValue)
                canonical.SeasonNumber = dto.SeasonNumber.Value;
            if (dto.Name is not null)
                canonical.Name = dto.Name;
            if (dto.Overview is not null)
                canonical.Overview = dto.Overview;

            canonical.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return MapCanonicalSeasonToDto(canonical);
        }

        // Fallback: old per-user
        var old = await _db.VideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        if (dto.SeasonNumber.HasValue)
            old.SeasonNumber = dto.SeasonNumber.Value;
        if (dto.Name is not null)
            old.Name = dto.Name;
        if (dto.Overview is not null)
            old.Overview = dto.Overview;

        old.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapSeasonToDto(old);
    }

    /// <inheritdoc />
    public async Task DeleteSeasonAsync(Guid seasonId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonical = await _db.CanonicalVideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (canonical is not null)
        {
            // Remove related episodes
            var episodes = await _db.CanonicalVideoEpisodes
                .Where(e => e.SeasonId == seasonId).ToListAsync(cancellationToken);
            _db.CanonicalVideoEpisodes.RemoveRange(episodes);
            _db.CanonicalVideoSeasons.Remove(canonical);

            // Update canonical series count
            var series = await _db.CanonicalVideoSeries.FindAsync(new object[] { canonical.SeriesId }, cancellationToken);
            if (series is not null)
            {
                series.TotalSeasons = await _db.CanonicalVideoSeasons
                    .CountAsync(s => s.SeriesId == canonical.SeriesId, cancellationToken);
                series.UpdatedAt = DateTime.UtcNow;
            }

            // Dual-write: soft-delete old season
            var oldSeason = await _db.VideoSeasons
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
            if (oldSeason is not null)
            {
                oldSeason.IsDeleted = true;
                oldSeason.DeletedAt = DateTime.UtcNow;
                oldSeason.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("CanonicalSeason {SeasonId} in series {SeriesId} deleted", seasonId, canonical.SeriesId);
            return;
        }

        // Fallback: old per-user
        var old = await _db.VideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        old.IsDeleted = true;
        old.DeletedAt = DateTime.UtcNow;
        old.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var oldSeries = await _db.VideoSeries.FindAsync(new object[] { old.SeriesId }, cancellationToken);
        if (oldSeries is not null)
        {
            oldSeries.TotalSeasons = await _db.VideoSeasons.CountAsync(s => s.SeriesId == old.SeriesId, cancellationToken);
            oldSeries.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Season {SeasonId} in series {SeriesId} soft-deleted", seasonId, old.SeriesId);
    }

    /// <inheritdoc />
    public async Task<VideoSeasonDto> FindOrCreateSeasonAsync(Guid seriesId, int seasonNumber, string? name, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonical = await _db.CanonicalVideoSeasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.SeriesId == seriesId && s.SeasonNumber == seasonNumber, cancellationToken);

        if (canonical is not null)
            return MapCanonicalSeasonToDto(canonical);

        canonical = new CanonicalVideoSeason
        {
            SeriesId = seriesId,
            SeasonNumber = seasonNumber,
            Name = name ?? $"Season {seasonNumber}"
        };

        _db.CanonicalVideoSeasons.Add(canonical);

        // ── Dual-write: old per-user season ──
        var oldSeries = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);
        if (oldSeries is not null)
        {
            var oldSeason = await _db.VideoSeasons
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId && s.SeasonNumber == seasonNumber, cancellationToken);

            if (oldSeason is null)
            {
                oldSeason = new VideoSeason
                {
                    SeriesId = seriesId,
                    SeasonNumber = seasonNumber,
                    Name = name ?? $"Season {seasonNumber}"
                };
                _db.VideoSeasons.Add(oldSeason);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return MapCanonicalSeasonToDto(canonical);
    }

    // ─── Episodes ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VideoEpisodeDto> AddEpisodeAsync(Guid seasonId, Guid videoId, int episodeNumber, string? title, string? overview, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve video's content hash
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(contentHash))
        {
            var oldVideo = await _db.Videos
                .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
            if (oldVideo is null)
                throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");
            contentHash = oldVideo.ContentHash ?? oldVideo.Id.ToString();
        }

        // Try canonical season
        var canonicalSeason = await _db.CanonicalVideoSeasons
            .Include(s => s.Series)
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (canonicalSeason is not null)
        {
            // Check if video is already in this season by content hash
            var alreadyInSeason = await _db.CanonicalVideoEpisodes
                .AnyAsync(e => e.SeasonId == seasonId && e.VideoContentHash == contentHash, cancellationToken);
            if (alreadyInSeason)
                throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeason, "Video is already an episode in this season.");

            // Avoid unique constraint violation on (SeasonId, EpisodeNumber) — auto-increment if taken
            var finalEpisodeNumber = episodeNumber;
            var episodeNumberExists = await _db.CanonicalVideoEpisodes
                .AnyAsync(e => e.SeasonId == seasonId && e.EpisodeNumber == finalEpisodeNumber, cancellationToken);
            if (episodeNumberExists)
            {
                finalEpisodeNumber = (await _db.CanonicalVideoEpisodes
                    .Where(e => e.SeasonId == seasonId)
                    .MaxAsync(e => (int?)e.EpisodeNumber, cancellationToken) ?? 0) + 1;
            }

            var maxOrder = await _db.CanonicalVideoEpisodes
                .Where(e => e.SeasonId == seasonId)
                .MaxAsync(e => (int?)e.SortOrder, cancellationToken) ?? 0;

            var episode = new CanonicalVideoEpisode
            {
                SeasonId = seasonId,
                VideoContentHash = contentHash,
                EpisodeNumber = finalEpisodeNumber,
                Title = title,
                Overview = overview,
                SortOrder = maxOrder + 1
            };

            _db.CanonicalVideoEpisodes.Add(episode);

            // ── Dual-write: old per-user episode ──
            var oldSeason = await _db.VideoSeasons
                .Include(s => s.Series)
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
            if (oldSeason is not null)
            {
                var newOldEpisode = new VideoEpisode
                {
                    SeasonId = seasonId,
                    VideoId = videoId,
                    EpisodeNumber = finalEpisodeNumber,
                    Title = title,
                    Overview = overview,
                    SortOrder = maxOrder + 1
                };
                _db.VideoEpisodes.Add(newOldEpisode);
            }

            // Save changes first so counts reflect database reality (Bug 10 fix)
            await _db.SaveChangesAsync(cancellationToken);

            // Update canonical season count after save
            canonicalSeason.EpisodeCount = await _db.CanonicalVideoEpisodes
                .CountAsync(e => e.SeasonId == seasonId, cancellationToken);
            canonicalSeason.UpdatedAt = DateTime.UtcNow;

            // Update canonical series totals after save
            var series = canonicalSeason.Series!;
            series.TotalEpisodes = await _db.CanonicalVideoEpisodes
                .CountAsync(e => e.Season!.SeriesId == series.Id, cancellationToken);
            series.UpdatedAt = DateTime.UtcNow;

            // Update old-per-user counts after save
            if (oldSeason is not null)
            {
                oldSeason.EpisodeCount = await _db.VideoEpisodes
                    .CountAsync(e => e.SeasonId == seasonId, cancellationToken);
                oldSeason.UpdatedAt = DateTime.UtcNow;

                if (oldSeason.Series is not null)
                {
                    oldSeason.Series.TotalEpisodes = await _db.VideoEpisodes
                        .CountAsync(e => e.Season!.SeriesId == oldSeason.Series.Id, cancellationToken);
                    oldSeason.Series.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Video {VideoId} added as episode {EpisodeNumber} to canonical season {SeasonId}",
                videoId, episodeNumber, seasonId);

            return new VideoEpisodeDto
            {
                Id = episode.Id,
                SeasonId = episode.SeasonId,
                VideoId = videoId,
                EpisodeNumber = episode.EpisodeNumber,
                Title = episode.Title,
                Overview = episode.Overview,
                SortOrder = episode.SortOrder
            };
        }

        // Fallback: old per-user tables
        var oldSeasonOnly = await _db.VideoSeasons
            .Include(s => s.Series)
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound, "Season not found.");

        var videoExists = await _db.Videos.AnyAsync(v => v.Id == videoId, cancellationToken);
        if (!videoExists)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInOldSeason = await _db.VideoEpisodes
            .AnyAsync(e => e.SeasonId == seasonId && e.VideoId == videoId, cancellationToken);
        if (alreadyInOldSeason)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeason, "Video is already an episode in this season.");

        var maxOrderOld = await _db.VideoEpisodes
            .Where(e => e.SeasonId == seasonId)
            .MaxAsync(e => (int?)e.SortOrder, cancellationToken) ?? 0;

        var oldEpisode = new VideoEpisode
        {
            SeasonId = seasonId,
            VideoId = videoId,
            EpisodeNumber = episodeNumber,
            Title = title,
            Overview = overview,
            SortOrder = maxOrderOld + 1
        };

        _db.VideoEpisodes.Add(oldEpisode);
        oldSeasonOnly.EpisodeCount = await _db.VideoEpisodes.CountAsync(e => e.SeasonId == seasonId, cancellationToken);
        oldSeasonOnly.UpdatedAt = DateTime.UtcNow;

        var oldSeriesOnly = oldSeasonOnly.Series!;
        oldSeriesOnly.TotalEpisodes = await _db.VideoEpisodes
            .CountAsync(e => e.Season!.SeriesId == oldSeriesOnly.Id, cancellationToken);
        oldSeriesOnly.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} added as episode {EpisodeNumber} to season {SeasonId} (old)",
            videoId, episodeNumber, seasonId);

        return new VideoEpisodeDto
        {
            Id = oldEpisode.Id,
            SeasonId = oldEpisode.SeasonId,
            VideoId = oldEpisode.VideoId,
            EpisodeNumber = oldEpisode.EpisodeNumber,
            Title = oldEpisode.Title,
            Overview = oldEpisode.Overview,
            SortOrder = oldEpisode.SortOrder
        };
    }

    /// <inheritdoc />
    public async Task RemoveEpisodeAsync(Guid seasonId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve video's content hash
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        // Try canonical — find episode by season AND content hash (Bug 9 fix)
        CanonicalVideoEpisode? canonicalEpisode = null;
        if (!string.IsNullOrEmpty(contentHash))
        {
            canonicalEpisode = await _db.CanonicalVideoEpisodes
                .FirstOrDefaultAsync(e => e.SeasonId == seasonId && e.VideoContentHash == contentHash, cancellationToken);
        }

        if (canonicalEpisode is not null)
        {
            _db.CanonicalVideoEpisodes.Remove(canonicalEpisode);

            // Dual-write: remove old episode
            var oldEpisode = await _db.VideoEpisodes
                .FirstOrDefaultAsync(e => e.SeasonId == seasonId && e.VideoId == videoId, cancellationToken);
            if (oldEpisode is not null)
                _db.VideoEpisodes.Remove(oldEpisode);

            // Save changes first so counts reflect database reality (Bug 10 fix)
            await _db.SaveChangesAsync(cancellationToken);

            // Update canonical season count after save
            var canonicalSeason = await _db.CanonicalVideoSeasons
                .Include(s => s.Series)
                .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
            if (canonicalSeason is not null)
            {
                canonicalSeason.EpisodeCount = await _db.CanonicalVideoEpisodes
                    .CountAsync(e => e.SeasonId == seasonId, cancellationToken);
                canonicalSeason.UpdatedAt = DateTime.UtcNow;

                if (canonicalSeason.Series is not null)
                {
                    canonicalSeason.Series.TotalEpisodes = await _db.CanonicalVideoEpisodes
                        .CountAsync(e => e.Season!.SeriesId == canonicalSeason.Series.Id, cancellationToken);
                    canonicalSeason.Series.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        // Fallback: old per-user tables
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
        // Try canonical
        var canonicalSeason = await _db.CanonicalVideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);

        if (canonicalSeason is not null)
        {
            var episodes = await _db.CanonicalVideoEpisodes
                .Where(e => e.SeasonId == seasonId)
                .OrderBy(e => e.EpisodeNumber)
                .ThenBy(e => e.SortOrder)
                .ToListAsync(cancellationToken);

            // Resolve user's videos by content hash
            var contentHashes = episodes.Select(e => e.VideoContentHash).ToHashSet();
            var userVideos = await _db.UserVideos
                .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
                .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && contentHashes.Contains(uv.CanonicalContentHash))
                .ToListAsync(cancellationToken);
            var userVideoByHash = userVideos.ToDictionary(uv => uv.CanonicalContentHash);

            return episodes.Select(e =>
            {
                VideoDto? videoDto = null;
                var videoId = Guid.Empty;

                if (userVideoByHash.TryGetValue(e.VideoContentHash, out var uv) && uv.CanonicalVideo is not null)
                {
                    videoId = uv.Id;
                    videoDto = new VideoDto
                    {
                        Id = uv.Id,
                        FileNodeId = uv.FileNodeId,
                        Title = uv.CanonicalVideo.Title,
                        FileName = uv.CanonicalVideo.FileName,
                        MimeType = uv.CanonicalVideo.MimeType,
                        SizeBytes = uv.CanonicalVideo.SizeBytes,
                        Duration = TimeSpan.FromTicks(uv.CanonicalVideo.DurationTicks),
                        Width = uv.CanonicalVideo.Metadata?.Width,
                        Height = uv.CanonicalVideo.Metadata?.Height,
                        IsFavorite = uv.IsFavorite,
                        ViewCount = uv.ViewCount,
                        CreatedAt = uv.CreatedAt
                    };
                }

                return new VideoEpisodeDto
                {
                    Id = e.Id,
                    SeasonId = e.SeasonId,
                    VideoId = videoId,
                    EpisodeNumber = e.EpisodeNumber,
                    Title = e.Title,
                    Overview = e.Overview,
                    SortOrder = e.SortOrder,
                    Video = videoDto
                };
            }).ToList();
        }

        // Fallback: old per-user
        var oldSeason = await _db.VideoSeasons
            .FirstOrDefaultAsync(s => s.Id == seasonId && s.Series!.OwnerId == caller.UserId, cancellationToken);

        if (oldSeason is null)
            return [];

        var oldEpisodes = await _db.VideoEpisodes
            .Include(e => e.Video).ThenInclude(v => v!.Metadata)
            .Where(e => e.SeasonId == seasonId)
            .OrderBy(e => e.EpisodeNumber)
            .ThenBy(e => e.SortOrder)
            .ToListAsync(cancellationToken);

        return oldEpisodes.Select(e => new VideoEpisodeDto
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
        // Try canonical series poster hash
        var canonicalPosterHash = await _db.CanonicalVideoSeries
            .Where(s => s.Id == seriesId)
            .Select(s => s.PosterHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(canonicalPosterHash))
        {
            // Read poster from content-addressed storage
            var prefixDir = canonicalPosterHash.Length >= 2 ? canonicalPosterHash[..2] : canonicalPosterHash;
            var posterDir = Path.Combine(_storageRoot, "images", prefixDir);

            if (Directory.Exists(posterDir))
            {
                var files = Directory.GetFiles(posterDir, $"{canonicalPosterHash}.*");
                if (files.Length > 0)
                {
                    try
                    {
                        return await File.ReadAllBytesAsync(files[0], cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read canonical poster for series {SeriesId}", seriesId);
                    }
                }
            }

            return null;
        }

        // Fallback: old per-user series
        var oldSeries = await _db.VideoSeries
            .Where(s => s.Id == seriesId && s.OwnerId == caller.UserId && !s.IsDeleted)
            .Select(s => new { s.ThumbnailPoster, s.HasExternalPoster, s.ExternalPosterPath })
            .FirstOrDefaultAsync(cancellationToken);

        if (oldSeries is null)
            return null;

        if (oldSeries.ThumbnailPoster is { Length: > 0 })
            return oldSeries.ThumbnailPoster;

        if (oldSeries.HasExternalPoster && !string.IsNullOrWhiteSpace(oldSeries.ExternalPosterPath))
        {
            try
            {
                if (File.Exists(oldSeries.ExternalPosterPath))
                    return await File.ReadAllBytesAsync(oldSeries.ExternalPosterPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cached poster for series {SeriesId}", seriesId);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task EnrichSeriesAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        // ── Load series ──
        var series = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (series is null)
        {
            _logger.LogDebug("Series {SeriesId} not found for enrichment", seriesId);
            return;
        }

        // Skip if already enriched (unless force is needed in future)
        if (series.TmdbId is not null && !string.IsNullOrEmpty(series.PosterHash))
        {
            _logger.LogDebug("Series {SeriesId} ('{Name}') already enriched, skipping", seriesId, series.Name);
            return;
        }

        try
        {
            if (series.Type == SeriesType.MovieFranchise)
            {
                await EnrichMovieFranchiseAsync(series, cancellationToken);
            }
            else
            {
                await EnrichTvSeriesAsync(series, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Series enrichment failed for {SeriesId} ('{Name}')", seriesId, series.Name);
        }
    }

    /// <summary>
    /// Enriches a TV series by searching TMDB, fetching details, and downloading the poster.
    /// </summary>
    private async Task EnrichTvSeriesAsync(CanonicalVideoSeries series, CancellationToken cancellationToken)
    {
        // Search TMDB for TV series by name
        var searchResults = await _tmdbClient.SearchTvSeriesAsync(series.Name, cancellationToken: cancellationToken);
        var match = searchResults?.FirstOrDefault();
        if (match is null)
        {
            _logger.LogDebug("No TMDB TV series match for '{Name}'", series.Name);
            return;
        }

        // Get full TV series details
        var detail = await _tmdbClient.GetTvSeriesAsync(match.Id, cancellationToken);
        if (detail is null)
        {
            _logger.LogDebug("TMDB TV series detail not found for id {TmdbId}", match.Id);
            return;
        }

        // Download poster
        string? posterHash = null;
        if (detail.PosterPath is not null)
        {
            var poster = await _tmdbClient.DownloadPosterAsync(detail.PosterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var ext = poster.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                posterHash = _contentStorage.Store(poster.Data, ext);
            }
        }

        // Update series with TMDB data
        series.TmdbId = match.Id;
        series.TmdbName = detail.Name;
        series.TmdbOverview = detail.Overview;
        series.TmdbRating = detail.VoteAverage;
        series.Genres = detail.Genres is { Count: > 0 }
            ? string.Join(", ", detail.Genres.Select(g => g.Name))
            : null;
        series.Status = detail.Status;
        series.TotalSeasons = detail.NumberOfSeasons;
        series.TotalEpisodes = detail.NumberOfEpisodes;
        series.PosterHash = posterHash;
        series.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("TV series '{Name}' (id={TmdbId}) enriched with poster", series.Name, match.Id);
    }

    /// <summary>
    /// Enriches a movie franchise by searching TMDB collections, fetching details, and downloading the poster.
    /// </summary>
    private async Task EnrichMovieFranchiseAsync(CanonicalVideoSeries series, CancellationToken cancellationToken)
    {
        // Search TMDB for collection by name
        var searchResults = await _tmdbClient.SearchCollectionAsync(series.Name, cancellationToken);
        var match = searchResults?.FirstOrDefault();
        if (match is null)
        {
            _logger.LogDebug("No TMDB collection match for '{Name}'", series.Name);
            return;
        }

        // Get full collection details
        var detail = await _tmdbClient.GetCollectionAsync(match.Id, cancellationToken);
        if (detail is null)
        {
            _logger.LogDebug("TMDB collection detail not found for id {TmdbId}", match.Id);
            return;
        }

        // Download poster
        string? posterHash = null;
        if (detail.PosterPath is not null)
        {
            var poster = await _tmdbClient.DownloadPosterAsync(detail.PosterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var ext = poster.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                posterHash = _contentStorage.Store(poster.Data, ext);
            }
        }

        // Update series with TMDB data
        series.TmdbId = match.Id;
        series.TmdbName = detail.Name;
        series.TmdbOverview = detail.Overview;
        series.PosterHash = posterHash;
        series.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Movie franchise '{Name}' (collectionId={TmdbId}) enriched with poster", series.Name, match.Id);
    }

    /// <inheritdoc />
    public async Task EnrichAllUnenrichedSeriesAsync(CancellationToken cancellationToken = default)
    {
        var unenrichedSeries = await _db.CanonicalVideoSeries
            .Where(s => s.TmdbId == null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Enriching {Count} unenriched series from TMDB", unenrichedSeries.Count);

        foreach (var series in unenrichedSeries)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await EnrichSeriesAsync(series.Id, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Series enrichment failed for {SeriesId} ('{Name}')", series.Id, series.Name);
            }
        }

        _logger.LogInformation("Series enrichment complete: {Count} series processed", unenrichedSeries.Count);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto?> FindSeriesByVideoIdAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve video's content hash
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(contentHash))
        {
            // Check canonical episodes
            var canonicalEpisodeSeries = await _db.CanonicalVideoEpisodes
                .Include(e => e.Season).ThenInclude(s => s!.Series)
                .Where(e => e.VideoContentHash == contentHash)
                .Select(e => e.Season!.Series)
                .FirstOrDefaultAsync(cancellationToken);

            if (canonicalEpisodeSeries is not null)
                return MapCanonicalToDto(canonicalEpisodeSeries);

            // Check canonical franchise items
            var canonicalItemSeries = await _db.CanonicalVideoSeriesItems
                .Include(i => i.Series)
                .Where(i => i.VideoContentHash == contentHash)
                .Select(i => i.Series)
                .FirstOrDefaultAsync(cancellationToken);

            if (canonicalItemSeries is not null)
                return MapCanonicalToDto(canonicalItemSeries);
        }

        // Fallback: old per-user tables
        var episodeSeries = await _db.VideoEpisodes
            .Include(e => e.Season).ThenInclude(s => s!.Series)
            .Where(e => e.VideoId == videoId && e.Season!.Series!.OwnerId == caller.UserId)
            .Select(e => e.Season!.Series)
            .FirstOrDefaultAsync(cancellationToken);

        if (episodeSeries is not null)
            return MapToDto(episodeSeries);

        var itemSeries = await _db.VideoSeriesItems
            .Include(i => i.Series)
            .Where(i => i.VideoId == videoId && i.Series!.OwnerId == caller.UserId)
            .Select(i => i.Series)
            .FirstOrDefaultAsync(cancellationToken);

        if (itemSeries is not null)
            return MapToDto(itemSeries);

        return null;
    }

    // ─── Auto-Detection ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesDto>> DetectSeriesFromLibraryAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Use UserVideos + CanonicalVideos for detection
        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .Where(uv => uv.OwnerId == caller.UserId && !uv.IsDeleted && uv.CanonicalVideo != null)
            .ToListAsync(cancellationToken);

        // Group by potential series names extracted from folder-like patterns in filenames
        var seriesGroups = new Dictionary<string, (List<Guid> VideoIds, SeriesType Type)>();

        foreach (var uv in userVideos)
        {
            var cv = uv.CanonicalVideo!;
            var (seriesName, seriesType) = DetectSeriesFromVideo(cv.FileName, cv.Title);
            if (seriesName is null)
                continue;

            if (!seriesGroups.ContainsKey(seriesName))
                seriesGroups[seriesName] = (new List<Guid>(), seriesType);

            seriesGroups[seriesName].VideoIds.Add(uv.Id);
        }

        // Also check old Videos table for migration compatibility
        var oldVideos = await _db.Videos
            .Where(v => v.OwnerId == caller.UserId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var video in oldVideos)
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
                continue;

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

    private static VideoSeriesDto MapCanonicalToDto(CanonicalVideoSeries series)
    {
        return new VideoSeriesDto
        {
            Id = series.Id,
            Name = series.Name,
            Description = series.Description,
            Type = series.Type.ToString(),
            TmdbRating = series.TmdbRating,
            Genres = series.Genres,
            Status = series.Status,
            TotalSeasons = series.Seasons?.Count ?? 0,
            TotalEpisodes = series.TotalEpisodes,
            HasExternalPoster = !string.IsNullOrEmpty(series.PosterHash),
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

    private static VideoSeasonDto MapCanonicalSeasonToDto(CanonicalVideoSeason season)
    {
        return new VideoSeasonDto
        {
            Id = season.Id,
            SeriesId = season.SeriesId,
            SeasonNumber = season.SeasonNumber,
            Name = season.Name,
            Overview = season.Overview,
            EpisodeCount = season.Episodes?.Count ?? 0,
            HasExternalPoster = !string.IsNullOrEmpty(season.PosterHash),
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
