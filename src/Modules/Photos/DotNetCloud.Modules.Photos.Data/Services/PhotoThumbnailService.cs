using DotNetCloud.Modules.Photos.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Generates and stores photo thumbnails (grid 300px, detail 1200px) in the database
/// using ImageSharp. Full-size requests return null (caller uses the original file).
/// </summary>
public sealed class PhotoThumbnailService : IPhotoThumbnailService
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif",
        "image/webp", "image/bmp", "image/tiff"
    };

    private readonly PhotosDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly ILogger<PhotoThumbnailService> _logger;

    /// <summary>
    /// Initializes the photo thumbnail service.
    /// </summary>
    public PhotoThumbnailService(
        PhotosDbContext db,
        IFileStorageEngine storageEngine,
        ILogger<PhotoThumbnailService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid photoId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        if (size == PhotoThumbnailSize.Full)
        {
            return (null, null);
        }

        // Read only the specific thumbnail column to avoid loading the other blob
        byte[]? data = size == PhotoThumbnailSize.Grid
            ? await _db.Photos.IgnoreQueryFilters()
                .Where(p => p.Id == photoId)
                .Select(p => p.ThumbnailGrid)
                .FirstOrDefaultAsync(cancellationToken)
            : await _db.Photos.IgnoreQueryFilters()
                .Where(p => p.Id == photoId)
                .Select(p => p.ThumbnailDetail)
                .FirstOrDefaultAsync(cancellationToken);

        if (data is null || data.Length == 0)
        {
            return (null, null);
        }

        Stream stream = new MemoryStream(data, writable: false);
        return (stream, "image/jpeg");
    }

    /// <inheritdoc />
    public async Task GenerateThumbnailsAsync(
        Guid photoId,
        string storagePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storagePath))
        {
            _logger.LogWarning("No storage path provided for photo thumbnail generation: {PhotoId}", photoId);
            return;
        }

        if (!SupportedMimeTypes.Contains(mimeType))
        {
            _logger.LogDebug("Unsupported MIME type for photo thumbnail: {MimeType}", mimeType);
            return;
        }

        try
        {
            using var sourceStream = await _storageEngine.OpenReadStreamAsync(storagePath, cancellationToken);
            if (sourceStream is null)
            {
                _logger.LogWarning("Source file not found in storage engine for photo thumbnail generation: {Path}", storagePath);
                return;
            }

            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            var gridData = await GenerateThumbnailBytesAsync(image, PhotoThumbnailSize.Grid, cancellationToken);
            var detailData = await GenerateThumbnailBytesAsync(image, PhotoThumbnailSize.Detail, cancellationToken);

            // Update the photo record with thumbnail data
            var photo = await _db.Photos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

            if (photo is null)
            {
                _logger.LogWarning("Photo {PhotoId} not found for thumbnail storage", photoId);
                return;
            }

            photo.ThumbnailGrid = gridData;
            photo.ThumbnailDetail = detailData;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Photo thumbnails generated and stored in DB for {PhotoId} (grid={GridSize}b, detail={DetailSize}b)",
                photoId, gridData?.Length ?? 0, detailData?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnails for photo {PhotoId} from storage {Path}", photoId, storagePath);
        }
    }

    /// <inheritdoc />
    public async Task DeleteThumbnailsAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

        if (photo is not null)
        {
            photo.ThumbnailGrid = null;
            photo.ThumbnailDetail = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Photo thumbnails deleted for {PhotoId}", photoId);
    }

    private static async Task<byte[]?> GenerateThumbnailBytesAsync(
        Image source,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken)
    {
        var maxDimension = (int)size;
        if (maxDimension <= 0)
        {
            return null;
        }

        using var clone = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max
            });
        });

        using var ms = new MemoryStream();
        await clone.SaveAsJpegAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
