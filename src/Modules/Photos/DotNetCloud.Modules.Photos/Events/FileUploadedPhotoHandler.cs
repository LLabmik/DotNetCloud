using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-create Photo records for image files.
/// Delegates to PhotoService in the Data layer for actual record creation.
/// </summary>
public sealed class FileUploadedPhotoHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/bmp", "image/tiff", "image/svg+xml", "image/heic", "image/heif"
    };

    private readonly IPhotoIndexingCallback? _indexingCallback;
    private readonly ILogger<FileUploadedPhotoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedPhotoHandler"/> class.
    /// </summary>
    public FileUploadedPhotoHandler(ILogger<FileUploadedPhotoHandler> logger, IPhotoIndexingCallback? indexingCallback = null)
    {
        _logger = logger;
        _indexingCallback = indexingCallback;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !ImageMimeTypes.Contains(@event.MimeType))
        {
            return;
        }

        if (_indexingCallback is not null)
        {
            try
            {
                await _indexingCallback.IndexPhotoAsync(
                    @event.FileNodeId, @event.FileName, @event.MimeType, @event.Size,
                    @event.UploadedByUserId, storagePath: @event.StoragePath, cancellationToken);

                _logger.LogInformation(
                    "Photo auto-created for uploaded image: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-create photo for uploaded image: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Image file uploaded: {FileName} ({MimeType}) by user {UserId} — indexing callback not registered",
                @event.FileName, @event.MimeType, @event.UploadedByUserId);
        }
    }

    /// <summary>
    /// Gets the set of image MIME types this handler recognizes.
    /// </summary>
    public static IReadOnlySet<string> SupportedMimeTypes => ImageMimeTypes;
}

/// <summary>
/// Callback interface for photo indexing — implemented in the Data layer, injected via DI.
/// Avoids direct dependency from Module → Data layer.
/// </summary>
public interface IPhotoIndexingCallback
{
    /// <summary>Indexes an uploaded image as a Photo record and generates thumbnails.</summary>
    /// <param name="fileNodeId">The Files-module node ID.</param>
    /// <param name="fileName">Display file name.</param>
    /// <param name="mimeType">MIME type of the image.</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="storagePath">Relative content-addressable storage path (for thumbnail generation via IFileStorageEngine).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexPhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of FileNode IDs already indexed in the photo library for the given owner.
    /// Used by the scanner to skip files that have not changed since the last scan.
    /// </summary>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes Photo records whose source FileNodes no longer exist in the Files module.
    /// Also removes related junction data (album membership, tags, shares, edit records).
    /// </summary>
    /// <param name="deletedFileNodeIds">FileNode IDs whose backing files have been deleted.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of photos removed.</returns>
    Task<int> RemoveDeletedPhotosAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all photo library metadata from the database (photos, albums, metadata, tags,
    /// shares, edit records). The actual image files are NOT affected.
    /// After calling this, a re-scan will rebuild the entire library from scratch.
    /// </summary>
    Task ResetCollectionAsync(CancellationToken cancellationToken = default);
}
