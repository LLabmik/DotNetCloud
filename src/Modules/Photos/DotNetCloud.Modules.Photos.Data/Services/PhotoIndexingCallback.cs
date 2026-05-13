using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Implements the photo indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedPhotoHandler when an image file is uploaded.
/// </summary>
public sealed class PhotoIndexingCallback : IPhotoIndexingCallback
{
    private readonly PhotoService _photoService;
    private readonly IPhotoThumbnailService _thumbnailService;
    private readonly PhotosDbContext _db;
    private readonly ILogger<PhotoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoIndexingCallback"/> class.
    /// </summary>
    public PhotoIndexingCallback(PhotoService photoService, IPhotoThumbnailService thumbnailService, PhotosDbContext db, ILogger<PhotoIndexingCallback> logger)
    {
        _photoService = photoService;
        _thumbnailService = thumbnailService;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexPhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        // Check for existing photo record (idempotent re-scan support)
        var existing = await _photoService.GetByFileNodeIdAsync(fileNodeId, ownerId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogDebug("Photo already exists for FileNode {FileNodeId} (PhotoId {PhotoId}), skipping insert", fileNodeId, existing.Id);

            // Still generate thumbnails for existing photos that may lack them
            if (!string.IsNullOrEmpty(storagePath))
            {
                try
                {
                    await _thumbnailService.GenerateThumbnailsAsync(existing.Id, storagePath, mimeType, cancellationToken);
                    _logger.LogDebug("Thumbnails regenerated for existing photo {PhotoId}", existing.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate thumbnails for existing photo {PhotoId} from {Path}", existing.Id, storagePath);
                }
            }

            return;
        }

        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        var photo = await _photoService.CreatePhotoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        _logger.LogDebug("Photo indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);

        // Generate thumbnails if we have the storage path
        if (!string.IsNullOrEmpty(storagePath))
        {
            try
            {
                await _thumbnailService.GenerateThumbnailsAsync(photo.Id, storagePath, mimeType, cancellationToken);
                _logger.LogDebug("Thumbnails generated for photo {PhotoId}", photo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate thumbnails for photo {PhotoId} from {Path}", photo.Id, storagePath);
            }
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.Photos
            .Where(p => p.OwnerId == ownerId)
            .Select(p => p.FileNodeId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <inheritdoc />
    public async Task<int> RemoveDeletedPhotosAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Where(p => p.OwnerId == ownerId && deletedFileNodeIds.Contains(p.FileNodeId) && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
            return 0;

        var photoIds = photos.Select(p => p.Id).ToHashSet();

        // Remove related junction/child records in FK-safe order.
        var editRecords = await _db.PhotoEditRecords
            .Where(e => photoIds.Contains(e.PhotoId))
            .ToListAsync(cancellationToken);
        _db.PhotoEditRecords.RemoveRange(editRecords);

        var shares = await _db.PhotoShares
            .Where(s => s.PhotoId.HasValue && photoIds.Contains(s.PhotoId.Value))
            .ToListAsync(cancellationToken);
        _db.PhotoShares.RemoveRange(shares);

        var tags = await _db.PhotoTags
            .Where(t => photoIds.Contains(t.PhotoId))
            .ToListAsync(cancellationToken);
        _db.PhotoTags.RemoveRange(tags);

        var albumPhotos = await _db.AlbumPhotos
            .Where(a => photoIds.Contains(a.PhotoId))
            .ToListAsync(cancellationToken);
        _db.AlbumPhotos.RemoveRange(albumPhotos);

        var metadatas = await _db.PhotoMetadata
            .Where(m => photoIds.Contains(m.PhotoId))
            .ToListAsync(cancellationToken);
        _db.PhotoMetadata.RemoveRange(metadatas);

        // Soft-delete the photo records.
        var now = DateTime.UtcNow;
        foreach (var photo in photos)
        {
            photo.IsDeleted = true;
            photo.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Removed {Count} deleted photo records for user {OwnerId}",
            photos.Count, ownerId);

        return photos.Count;
    }

    /// <inheritdoc />
    public async Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting photo library metadata for owner {OwnerId}", ownerId);

        // Get all photo IDs owned by this user
        var ownedPhotoIds = await _db.Photos
            .IgnoreQueryFilters()
            .Where(p => p.OwnerId == ownerId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var ownedPhotoIdSet = ownedPhotoIds.ToHashSet();

        // Get owner's album IDs (albums typically belong to owner)
        var ownedAlbumIds = await _db.Albums
            .IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Delete in FK-safe order: children/junctions first, then parents.
        // All deletes scoped to ownerId.
        var editRecords = await _db.PhotoEditRecords.IgnoreQueryFilters()
            .Where(r => ownedPhotoIdSet.Contains(r.PhotoId)).ToListAsync(cancellationToken);
        _db.PhotoEditRecords.RemoveRange(editRecords);

        var shares = await _db.PhotoShares.IgnoreQueryFilters()
            .Where(s => s.PhotoId.HasValue && ownedPhotoIdSet.Contains(s.PhotoId.Value)).ToListAsync(cancellationToken);
        _db.PhotoShares.RemoveRange(shares);

        var tags = await _db.PhotoTags.IgnoreQueryFilters()
            .Where(t => ownedPhotoIdSet.Contains(t.PhotoId)).ToListAsync(cancellationToken);
        _db.PhotoTags.RemoveRange(tags);

        var albumPhotos = await _db.AlbumPhotos.IgnoreQueryFilters()
            .Where(ap => ownedAlbumIds.Contains(ap.AlbumId)).ToListAsync(cancellationToken);
        _db.AlbumPhotos.RemoveRange(albumPhotos);

        var metadata = await _db.PhotoMetadata.IgnoreQueryFilters()
            .Where(m => ownedPhotoIdSet.Contains(m.PhotoId)).ToListAsync(cancellationToken);
        _db.PhotoMetadata.RemoveRange(metadata);

        var photos = await _db.Photos.IgnoreQueryFilters()
            .Where(p => p.OwnerId == ownerId).ToListAsync(cancellationToken);
        _db.Photos.RemoveRange(photos);

        var albums = await _db.Albums.IgnoreQueryFilters()
            .Where(a => a.OwnerId == ownerId).ToListAsync(cancellationToken);
        _db.Albums.RemoveRange(albums);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("RESET complete for owner {OwnerId}: {PhotoCount} photos, {AlbumCount} albums",
            ownerId, photos.Count, albums.Count);
    }
}
