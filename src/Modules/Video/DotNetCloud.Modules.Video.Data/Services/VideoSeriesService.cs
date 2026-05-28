using System.IO;
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
/// Uses canonical (shared) tables for content deduplication with dual-write
/// to old per-user tables for backward compatibility.
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

        // Check canonical series by name (shared across users)
        var existingCanonical = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Name == normalizedName, cancellationToken);

        if (existingCanonical is not null)
        {
            // Dual-write: create old-format series for this user
            var oldSeries = new VideoSeries
            {
                OwnerId = caller.UserId,
                Name = normalizedName,
                Description = dto.Description,
                Type = ParseSeriesType(dto.Type),
                Year = dto.Year
            };
            _db.VideoSeries.Add(oldSeries);
            await _db.SaveChangesAsync(cancellationToken);
            return MapToDto(oldSeries);
        }

        // ── Create canonical series (shared) ──
        var canonicalSeries = new CanonicalVideoSeries
        {
            Name = normalizedName,
            Description = dto.Description,
            Type = ParseSeriesType(dto.Type)
        };
        _db.CanonicalVideoSeries.Add(canonicalSeries);

        // ── Dual-write: old per-user series ──
        var oldSeriesNew = new VideoSeries
        {
            OwnerId = caller.UserId,
            Name = normalizedName,
            Description = dto.Description,
            Type = ParseSeriesType(dto.Type),
            Year = dto.Year
        };
        _db.VideoSeries.Add(oldSeriesNew);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeries {SeriesId} / old Series {OldSeriesId} '{Name}' ({Type}) created by user {UserId}",
            canonicalSeries.Id, oldSeriesNew.Id, normalizedName, canonicalSeries.Type, caller.UserId);

        return MapToDto(oldSeriesNew);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto?> GetSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical series first
        var canonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonical is not null)
            return MapCanonicalToDto(canonical);

        // Fallback: old per-user series
        var old = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        return old is null ? null : MapToDto(old);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesDto>> ListSeriesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        // List all canonical series
        var canonicalSeries = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var result = canonicalSeries
            .Where(s => s.TotalEpisodes > 1 || (s.Items?.Count ?? 0) > 1)
            .Select(MapCanonicalToDto)
            .ToList();

        // Also include old per-user series that don't have a canonical equivalent
        var oldSeries = await _db.VideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .Where(s => s.OwnerId == caller.UserId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var canonicalNames = canonicalSeries.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var old in oldSeries)
        {
            if (!canonicalNames.Contains(old.Name))
            {
                result.Add(MapToDto(old));
            }
        }

        return result
            .Where(s => s.TotalEpisodes > 1)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> UpdateSeriesAsync(Guid seriesId, UpdateVideoSeriesDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical series first
        var canonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonical is not null)
        {
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

        // Fallback: old per-user series
        var old = await _db.VideoSeries
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
            old.Name = normalizedName;
        }

        if (dto.Description is not null)
            old.Description = dto.Description;
        if (dto.Type is not null)
            old.Type = ParseSeriesType(dto.Type);
        if (dto.Year.HasValue)
            old.Year = dto.Year;

        old.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(old);
    }

    /// <inheritdoc />
    public async Task DeleteSeriesAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical series — no owner check (shared)
        var canonical = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonical is not null)
        {
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
            return;
        }

        // Fallback: old per-user series
        var old = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        old.IsDeleted = true;
        old.DeletedAt = DateTime.UtcNow;
        old.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Series {SeriesId} '{Name}' soft-deleted by user {UserId}",
            seriesId, old.Name, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<VideoSeriesDto> FindOrCreateByNameAsync(string name, string type, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();
        var seriesType = ParseSeriesType(type);

        // Check canonical series first (shared)
        var existingCanonical = await _db.CanonicalVideoSeries
            .Include(s => s.Seasons)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Name == normalizedName, cancellationToken);

        if (existingCanonical is not null)
        {
            // Dual-write: ensure old per-user series exists
            var oldExisting = await _db.VideoSeries
                .FirstOrDefaultAsync(s => s.Name == normalizedName && s.OwnerId == caller.UserId && !s.IsDeleted, cancellationToken);

            if (oldExisting is null)
            {
                oldExisting = new VideoSeries
                {
                    OwnerId = caller.UserId,
                    Name = normalizedName,
                    Type = seriesType
                };
                _db.VideoSeries.Add(oldExisting);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return MapCanonicalToDto(existingCanonical);
        }

        // ── Create canonical series ──
        var canonical = new CanonicalVideoSeries
        {
            Name = normalizedName,
            Type = seriesType
        };
        _db.CanonicalVideoSeries.Add(canonical);

        // ── Dual-write: old per-user series ──
        var oldSeries = new VideoSeries
        {
            OwnerId = caller.UserId,
            Name = normalizedName,
            Type = seriesType
        };
        _db.VideoSeries.Add(oldSeries);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSeries {SeriesId} / old Series {OldSeriesId} '{Name}' auto-created for user {UserId}",
            canonical.Id, oldSeries.Id, normalizedName, caller.UserId);

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
        {
            // Fallback: check old Video table
            var oldVideo = await _db.Videos
                .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);
            if (oldVideo is null)
                throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");
            contentHash = oldVideo.ContentHash ?? oldVideo.Id.ToString();
        }

        // Check canonical series
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonicalSeries is not null)
        {
            // Check if already in canonical series
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

            // ── Dual-write: old VideoSeriesItem ──
            var oldSeries = await _db.VideoSeries
                .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);
            if (oldSeries is not null)
            {
                var newOldItem = new VideoSeriesItem
                {
                    SeriesId = seriesId,
                    VideoId = videoId,
                    SortOrder = maxOrder,
                    EpisodeTitle = episodeTitle
                };
                _db.VideoSeriesItems.Add(newOldItem);
                oldSeries.UpdatedAt = DateTime.UtcNow;
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

        // Fallback: old per-user tables
        var oldSeriesOnly = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var videoExists = await _db.Videos.AnyAsync(v => v.Id == videoId, cancellationToken);
        if (!videoExists)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInOldSeries = await _db.VideoSeriesItems
            .AnyAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken);
        if (alreadyInOldSeries)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInSeries, "Video is already in this series.");

        var maxOrderOld = sortOrder ?? (await _db.VideoSeriesItems
            .Where(i => i.SeriesId == seriesId)
            .MaxAsync(i => (int?)i.SortOrder, cancellationToken) ?? -1) + 1;

        var oldItem = new VideoSeriesItem
        {
            SeriesId = seriesId,
            VideoId = videoId,
            SortOrder = maxOrderOld,
            EpisodeTitle = episodeTitle
        };

        _db.VideoSeriesItems.Add(oldItem);
        oldSeriesOnly.UpdatedAt = DateTime.UtcNow;

        if (sortOrder.HasValue)
        {
            var existingItems = await _db.VideoSeriesItems
                .Where(i => i.SeriesId == seriesId && i.Id != oldItem.Id && i.SortOrder >= sortOrder.Value)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingItems)
                existing.SortOrder++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new VideoSeriesItemDto
        {
            Id = oldItem.Id,
            SeriesId = oldItem.SeriesId,
            VideoId = oldItem.VideoId,
            SortOrder = oldItem.SortOrder,
            EpisodeTitle = oldItem.EpisodeTitle
        };
    }

    /// <inheritdoc />
    public async Task RemoveVideoFromSeriesAsync(Guid seriesId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var item = await _db.CanonicalVideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId, cancellationToken);

        if (item is not null)
        {
            _db.CanonicalVideoSeriesItems.Remove(item);

            // Dual-write: remove from old table
            var oldItem = await _db.VideoSeriesItems
                .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken);
            if (oldItem is not null)
                _db.VideoSeriesItems.Remove(oldItem);

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Fallback: old per-user tables
        var oldSeries = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var oldItemOnly = await _db.VideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        _db.VideoSeriesItems.Remove(oldItemOnly);
        oldSeries.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReorderSeriesItemAsync(Guid seriesId, Guid videoId, int newSortOrder, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonicalItem = await _db.CanonicalVideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId, cancellationToken);

        if (canonicalItem is not null)
        {
            canonicalItem.SortOrder = newSortOrder;

            // Dual-write: reorder old item
            var oldItem = await _db.VideoSeriesItems
                .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken);
            if (oldItem is not null)
                oldItem.SortOrder = newSortOrder;

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Fallback: old per-user tables
        var oldSeries = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var oldItemOnly = await _db.VideoSeriesItems
            .FirstOrDefaultAsync(i => i.SeriesId == seriesId && i.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this series.");

        var oldOrder = oldItemOnly.SortOrder;
        oldItemOnly.SortOrder = newSortOrder;
        oldSeries.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} reordered in series {SeriesId}: {OldOrder} -> {NewOrder}",
            videoId, seriesId, oldOrder, newSortOrder);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VideoSeriesItemDto>> GetSeriesVideosAsync(Guid seriesId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId, cancellationToken);

        if (canonicalSeries is not null)
        {
            var items = await _db.CanonicalVideoSeriesItems
                .Where(i => i.SeriesId == seriesId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync(cancellationToken);

            return items.Select(i => new VideoSeriesItemDto
            {
                Id = i.Id,
                SeriesId = i.SeriesId,
                VideoId = Guid.Empty, // Canonical items don't have a per-user video ID
                SortOrder = i.SortOrder,
                EpisodeTitle = i.EpisodeTitle
            }).ToList();
        }

        // Fallback: old per-user tables
        var oldSeries = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        if (oldSeries is null)
            return [];

        var oldItems = await _db.VideoSeriesItems
            .Include(i => i.Video).ThenInclude(v => v!.Metadata)
            .Where(i => i.SeriesId == seriesId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(cancellationToken);

        return oldItems.Select(i => new VideoSeriesItemDto
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
        // Check canonical series
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(s => s.Id == dto.SeriesId, cancellationToken);

        if (canonicalSeries is not null)
        {
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

            // ── Dual-write: old per-user season ──
            var oldSeries = await _db.VideoSeries
                .FirstOrDefaultAsync(s => s.Id == dto.SeriesId && s.OwnerId == caller.UserId, cancellationToken);
            if (oldSeries is not null)
            {
                var newOldSeason = new VideoSeason
                {
                    SeriesId = dto.SeriesId,
                    SeasonNumber = dto.SeasonNumber,
                    Name = dto.Name,
                    Overview = dto.Overview
                };
                _db.VideoSeasons.Add(newOldSeason);
                oldSeries.TotalSeasons = await _db.VideoSeasons.CountAsync(s => s.SeriesId == dto.SeriesId, cancellationToken);
                oldSeries.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("CanonicalSeason {SeasonNumber} created in series {SeriesId}",
                dto.SeasonNumber, dto.SeriesId);

            return MapCanonicalSeasonToDto(season);
        }

        // Fallback: old per-user tables
        var series = await _db.VideoSeries
            .FirstOrDefaultAsync(s => s.Id == dto.SeriesId && s.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var duplicateOldSeason = await _db.VideoSeasons
            .AnyAsync(s => s.SeriesId == dto.SeriesId && s.SeasonNumber == dto.SeasonNumber, cancellationToken);
        if (duplicateOldSeason)
            throw new BusinessRuleException(ErrorCodes.VideoSeasonNotFound,
                $"Season {dto.SeasonNumber} already exists in this series.");

        var oldSeason = new VideoSeason
        {
            SeriesId = dto.SeriesId,
            SeasonNumber = dto.SeasonNumber,
            Name = dto.Name,
            Overview = dto.Overview
        };

        _db.VideoSeasons.Add(oldSeason);
        series.TotalSeasons = await _db.VideoSeasons.CountAsync(s => s.SeriesId == dto.SeriesId, cancellationToken);
        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapSeasonToDto(oldSeason);
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
            canonicalSeason.EpisodeCount = await _db.CanonicalVideoEpisodes
                .CountAsync(e => e.SeasonId == seasonId, cancellationToken);
            canonicalSeason.UpdatedAt = DateTime.UtcNow;

            // Update canonical series totals
            var series = canonicalSeason.Series!;
            series.TotalEpisodes = await _db.CanonicalVideoEpisodes
                .CountAsync(e => e.Season!.SeriesId == series.Id, cancellationToken);
            series.UpdatedAt = DateTime.UtcNow;

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
        // Try canonical
        var canonicalEpisode = await _db.CanonicalVideoEpisodes
            .FirstOrDefaultAsync(e => e.SeasonId == seasonId, cancellationToken);

        if (canonicalEpisode is not null)
        {
            _db.CanonicalVideoEpisodes.Remove(canonicalEpisode);

            // Dual-write: remove old episode
            var oldEpisode = await _db.VideoEpisodes
                .FirstOrDefaultAsync(e => e.SeasonId == seasonId && e.VideoId == videoId, cancellationToken);
            if (oldEpisode is not null)
                _db.VideoEpisodes.Remove(oldEpisode);

            // Update canonical season count
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
            }

            await _db.SaveChangesAsync(cancellationToken);
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

            return episodes.Select(e => new VideoEpisodeDto
            {
                Id = e.Id,
                SeasonId = e.SeasonId,
                VideoId = Guid.Empty,
                EpisodeNumber = e.EpisodeNumber,
                Title = e.Title,
                Overview = e.Overview,
                SortOrder = e.SortOrder
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
            // Content-addressed storage path (external caller doesn't have access to _contentStorage)
            // Return null; the caller should use GetThumbnailAsync path for content-addressed retrieval
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
