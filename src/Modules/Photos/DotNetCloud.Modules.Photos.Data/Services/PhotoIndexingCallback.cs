using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Services;
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
    private readonly ILogger<PhotoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoIndexingCallback"/> class.
    /// </summary>
    public PhotoIndexingCallback(PhotoService photoService, IPhotoThumbnailService thumbnailService, ILogger<PhotoIndexingCallback> logger)
    {
        _photoService = photoService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexPhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        // Check for existing photo record (idempotent re-scan support)
        var existing = await _photoService.GetByFileNodeIdAsync(fileNodeId, cancellationToken);
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
}
