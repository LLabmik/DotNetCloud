using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video collections — CRUD, add/remove videos, ordering.
/// </summary>
public sealed class VideoCollectionService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoCollectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoCollectionService"/> class.
    /// </summary>
    public VideoCollectionService(VideoDbContext db, ILogger<VideoCollectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new video collection.
    /// </summary>
    public async Task<VideoCollectionDto> CreateCollectionAsync(CreateVideoCollectionDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = new VideoCollection
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            Description = dto.Description
        };

        _db.VideoCollections.Add(collection);
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
        var collection = await _db.VideoCollections
            .Include(c => c.Items).ThenInclude(ci => ci.Video)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken);

        return collection is null ? null : MapToDto(collection);
    }

    /// <summary>
    /// Lists collections for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<VideoCollectionDto>> ListCollectionsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collections = await _db.VideoCollections
            .Include(c => c.Items).ThenInclude(ci => ci.Video)
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
        var collection = await _db.VideoCollections
            .Include(c => c.Items).ThenInclude(ci => ci.Video)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        if (dto.Name is not null) collection.Name = dto.Name;
        if (dto.Description is not null) collection.Description = dto.Description;
        collection.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(collection);
    }

    /// <summary>
    /// Deletes a collection (soft delete).
    /// </summary>
    public async Task DeleteCollectionAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.VideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        collection.IsDeleted = true;
        collection.DeletedAt = DateTime.UtcNow;
        collection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Collection {CollectionId} soft-deleted by user {UserId}", collectionId, caller.UserId);
    }

    /// <summary>
    /// Adds a video to a collection.
    /// </summary>
    public async Task AddVideoAsync(Guid collectionId, Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.VideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        var videoExists = await _db.Videos.AnyAsync(v => v.Id == videoId, cancellationToken);
        if (!videoExists)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        var alreadyInCollection = await _db.VideoCollectionItems
            .AnyAsync(ci => ci.CollectionId == collectionId && ci.VideoId == videoId, cancellationToken);
        if (alreadyInCollection)
            throw new BusinessRuleException(ErrorCodes.VideoAlreadyInCollection, "Video is already in this collection.");

        var maxOrder = await _db.VideoCollectionItems
            .Where(ci => ci.CollectionId == collectionId)
            .MaxAsync(ci => (int?)ci.SortOrder, cancellationToken) ?? -1;

        _db.VideoCollectionItems.Add(new VideoCollectionItem
        {
            CollectionId = collectionId,
            VideoId = videoId,
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
        var collection = await _db.VideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoCollectionNotFound, "Collection not found.");

        var item = await _db.VideoCollectionItems
            .FirstOrDefaultAsync(ci => ci.CollectionId == collectionId && ci.VideoId == videoId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video is not in this collection.");

        _db.VideoCollectionItems.Remove(item);
        collection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets ordered video DTOs for a collection.
    /// </summary>
    public async Task<IReadOnlyList<VideoDto>> GetCollectionVideosAsync(Guid collectionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var collection = await _db.VideoCollections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.OwnerId == caller.UserId, cancellationToken);

        if (collection is null) return [];

        var videos = await _db.VideoCollectionItems
            .Include(ci => ci.Video).ThenInclude(v => v!.Metadata)
            .Where(ci => ci.CollectionId == collectionId)
            .OrderBy(ci => ci.SortOrder)
            .Select(ci => ci.Video!)
            .ToListAsync(cancellationToken);

        return videos.Select(v => MapVideoToDto(v, caller.UserId)).ToList();
    }

    private VideoCollectionDto MapToDto(VideoCollection collection)
    {
        var totalDurationTicks = collection.Items?
            .Sum(ci => ci.Video?.DurationTicks ?? 0) ?? 0;

        return new VideoCollectionDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            VideoCount = collection.Items?.Count ?? 0,
            TotalDuration = TimeSpan.FromTicks(totalDurationTicks),
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt
        };
    }

    private VideoDto MapVideoToDto(Models.Video video, Guid userId)
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
