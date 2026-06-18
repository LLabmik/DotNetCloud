using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video collections — CRUD, add/remove videos, ordering.
/// Uses the per-user video collection model (user_video_collections / user_video_collection_items).
/// </summary>
public sealed class VideoCollectionService : IVideoCollectionService
{
    private readonly VideoDbContext _db;
    private readonly IVideoSeriesService _seriesService;
    private readonly ILogger<VideoCollectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoCollectionService"/> class.
    /// </summary>
    public VideoCollectionService(VideoDbContext db, IVideoSeriesService seriesService, ILogger<VideoCollectionService> logger)
    {
        _db = db;
        _seriesService = seriesService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new video collection.
    /// </summary>
    public async Task<VideoCollectionDto> CreateCollectionAsync(CreateVideoCollectionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = new UserVideoCollection
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            Description = dto.Description
        };

        _db.UserVideoCollections.Add(collection);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Collection {CollectionId} '{Name}' created by user {UserId}",
            collection.Id, collection.Name, caller.UserId);

        return MapToDto(collection);
    }

    /// <summary>
    /// Gets a collection by ID.
    /// </summary>
    public async Task<VideoCollectionDto?> GetCollectionAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken);

        return collection is null ? null : MapToDto(collection);
    }

    /// <summary>
    /// Lists collections for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<VideoCollectionDto>> ListCollectionsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collections = await _db.UserVideoCollections
            .Include(c => c.Items)
            .Where(c => c.OwnerId == caller.UserId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return collections.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Updates a collection.
    /// </summary>
    public async Task<VideoCollectionDto> UpdateCollectionAsync(Guid collectionId, UpdateVideoCollectionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        if (dto.Name is not null)
            collection.Name = dto.Name;
        if (dto.Description is not null)
            collection.Description = dto.Description;
        collection.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(collection);
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    public async Task DeleteCollectionAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        _db.UserVideoCollections.Remove(collection);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Collection {CollectionId} deleted by user {UserId}", collectionId, caller.UserId);
    }

    /// <summary>
    /// Adds a video to a collection. The videoId is the UserVideo.Id (per-user video record).
    /// </summary>
    public async Task AddVideoAsync(Guid collectionId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInCollection = await _db.UserVideoCollectionItems
            .AnyAsync(ci => ci.CollectionId == collectionId && ci.CanonicalContentHash == userVideo.CanonicalContentHash, cancellationToken);
        if (alreadyInCollection)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInCollection, "Video is already in this collection.");

        var maxOrder = await _db.UserVideoCollectionItems
            .Where(ci => ci.CollectionId == collectionId)
            .MaxAsync(ci => (int?)ci.SortOrder, cancellationToken) ?? -1;

        _db.UserVideoCollectionItems.Add(new UserVideoCollectionItem
        {
            CollectionId = collectionId,
            CanonicalContentHash = userVideo.CanonicalContentHash,
            SortOrder = maxOrder + 1
        });

        collection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a video from a collection.
    /// </summary>
    public async Task RemoveVideoAsync(Guid collectionId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var item = await _db.UserVideoCollectionItems
            .FirstOrDefaultAsync(ci => ci.CollectionId == collectionId && ci.CanonicalContentHash == userVideo.CanonicalContentHash, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this collection.");

        _db.UserVideoCollectionItems.Remove(item);
        collection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets ordered video DTOs for a collection.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetCollectionVideosAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken);

        if (collection is null)
            return [];

        var items = await _db.UserVideoCollectionItems
            .Where(ci => ci.CollectionId == collectionId)
            .OrderBy(ci => ci.SortOrder)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return [];

        var contentHashes = items.Select(ci => ci.CanonicalContentHash).ToList();

        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && contentHashes.Contains(uv.CanonicalContentHash))
            .ToListAsync(cancellationToken);

        var userVideoLookup = userVideos.ToDictionary(uv => uv.CanonicalContentHash);

        return items
            .Select(ci => userVideoLookup.TryGetValue(ci.CanonicalContentHash, out var uv)
                ? MapFromCanonical(uv, uv.CanonicalVideo!)
                : null)
            .Where(v => v is not null)
            .Cast<VideoDto>()
            .ToList();
    }

    /// <summary>
    /// Gets collection content with series grouping: videos that belong to a series are replaced
    /// by their parent series card. Standalone videos are returned individually.
    /// </summary>
    public async Task<VideoCollectionContentDto> GetCollectionContentAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.UserVideoCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken);

        if (collection is null)
            return new VideoCollectionContentDto
            {
                Collection = new VideoCollectionDto
                {
                    Id = collectionId,
                    Name = "Unknown",
                    CreatedAt = DateTime.UtcNow
                }
            };

        var items = collection.Items?
            .OrderBy(ci => ci.SortOrder)
            .ToList() ?? [];

        if (items.Count == 0)
        {
            return new VideoCollectionContentDto
            {
                Collection = MapToDto(collection),
                Series = [],
                StandaloneVideos = [],
                TotalItems = 0
            };
        }

        var contentHashes = items.Select(ci => ci.CanonicalContentHash).ToList();

        var userVideos = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo).ThenInclude(cv => cv!.Metadata)
            .Where(uv => uv.OwnerId == caller.UserId && contentHashes.Contains(uv.CanonicalContentHash))
            .ToListAsync(cancellationToken);

        var userVideoLookup = userVideos.ToDictionary(uv => uv.CanonicalContentHash);

        // Find which content hashes in this collection belong to a series (episodes or franchise items)
        // Scope queries to only the collection's content hashes for performance (Bug 5 fix)
        var episodeHashes = await _db.CanonicalVideoEpisodes
            .Where(e => contentHashes.Contains(e.VideoContentHash))
            .Select(e => e.VideoContentHash)
            .ToListAsync(cancellationToken);
        var franchiseHashes = await _db.CanonicalVideoSeriesItems
            .Where(i => contentHashes.Contains(i.VideoContentHash))
            .Select(i => i.VideoContentHash)
            .ToListAsync(cancellationToken);
        var seriesContentHashSet = episodeHashes.Concat(franchiseHashes).ToHashSet();

        var standaloneVideos = items
            .Where(ci => !seriesContentHashSet.Contains(ci.CanonicalContentHash))
            .Select(ci => userVideoLookup.TryGetValue(ci.CanonicalContentHash, out var uv)
                ? MapFromCanonical(uv, uv.CanonicalVideo!)
                : null)
            .Where(v => v is not null)
            .Cast<VideoDto>()
            .ToList();

        // Find series for the content hashes that belong to series (Bug 1 fix)
        var seriesList = new List<VideoSeriesDto>();
        if (seriesContentHashSet.Count > 0)
        {
            var episodeSeriesIds = await _db.CanonicalVideoEpisodes
                .Where(e => seriesContentHashSet.Contains(e.VideoContentHash))
                .Select(e => e.Season!.SeriesId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var franchiseSeriesIds = await _db.CanonicalVideoSeriesItems
                .Where(i => seriesContentHashSet.Contains(i.VideoContentHash))
                .Select(i => i.SeriesId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allSeriesIds = episodeSeriesIds.Concat(franchiseSeriesIds).Distinct().ToList();
            if (allSeriesIds.Count > 0)
            {
                var canonicalSeries = await _db.CanonicalVideoSeries
                    .Include(s => s.Seasons)
                    .Include(s => s.Items)
                    .Where(s => allSeriesIds.Contains(s.Id))
                    .OrderBy(s => s.Name)
                    .ToListAsync(cancellationToken);

                seriesList = canonicalSeries.Select(s => new VideoSeriesDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Type = s.Type.ToString(),
                    TmdbRating = s.TmdbRating,
                    Genres = s.Genres,
                    Status = s.Status,
                    TotalSeasons = s.Seasons?.Count ?? 0,
                    TotalEpisodes = s.TotalEpisodes,
                    HasExternalPoster = !string.IsNullOrEmpty(s.PosterHash),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList();
            }
        }

        return new VideoCollectionContentDto
        {
            Collection = MapToDto(collection),
            Series = seriesList,
            StandaloneVideos = standaloneVideos,
            TotalItems = items.Count
        };
    }

    /// <summary>
    /// Finds a collection by name for the caller, or creates one if it doesn't exist.
    /// </summary>
    public async Task<VideoCollectionDto> FindOrCreateByNameAsync(string name, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserVideoCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Name == name && c.OwnerId == caller.UserId, cancellationToken);

        if (existing is not null)
            return MapToDto(existing);

        var collection = new UserVideoCollection
        {
            OwnerId = caller.UserId,
            Name = name,
            Description = null
        };

        _db.UserVideoCollections.Add(collection);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Collection {CollectionId} '{Name}' auto-created by FindOrCreateByName for user {UserId}",
            collection.Id, collection.Name, caller.UserId);

        return MapToDto(collection);
    }

    private VideoCollectionDto MapToDto(UserVideoCollection collection)
    {
        return new VideoCollectionDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            VideoCount = collection.Items?.Count ?? 0,
            TotalDuration = TimeSpan.Zero,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt
        };
    }

    private VideoDto MapFromCanonical(UserVideo userVideo, CanonicalVideo canonical)
    {
        // Load TMDB data from canonical enrichment — check both enrichment TmdbId and embedded TmdbId
        string? overview = null;
        double? tmdbRating = null;
        string? genres = null;
        DateTime? releaseDate = null;
        bool hasExternalPoster = canonical.HasExternalPoster;
        string? tmdbTagline = null;
        int? tmdbVoteCount = null;
        string? tmdbOriginalLanguage = null;
        string? tmdbOriginalTitle = null;

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
            WatchPositionTicks = null,
            CreatedAt = userVideo.CreatedAt,
            HasExternalPoster = hasExternalPoster,
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
}
