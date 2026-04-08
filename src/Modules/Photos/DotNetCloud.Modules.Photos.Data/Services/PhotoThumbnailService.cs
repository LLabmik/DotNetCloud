using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    private readonly IDownloadService _downloadService;
    private readonly ILogger<PhotoThumbnailService> _logger;

    /// <summary>
    /// Initializes the photo thumbnail service.
    /// </summary>
    public PhotoThumbnailService(
        PhotosDbContext db,
        IFileStorageEngine storageEngine,
        IDownloadService downloadService,
        ILogger<PhotoThumbnailService> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _downloadService = downloadService;
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

    /// <inheritdoc />
    public async Task<bool> SaveEditsAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Look up the photo to get FileNodeId + MimeType
            var photo = await _db.Photos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

            if (photo is null)
            {
                _logger.LogWarning("SaveEdits: Photo {PhotoId} not found", photoId);
                return false;
            }

            if (!SupportedMimeTypes.Contains(photo.MimeType))
            {
                _logger.LogWarning("SaveEdits: Unsupported MIME type {MimeType} for photo {PhotoId}", photo.MimeType, photoId);
                return false;
            }

            // 2. Load the edit stack
            var editRecords = await _db.PhotoEditRecords
                .Where(e => e.PhotoId == photoId)
                .OrderBy(e => e.StackOrder)
                .ToListAsync(cancellationToken);

            if (editRecords.Count == 0)
            {
                _logger.LogDebug("SaveEdits: No edits to apply for photo {PhotoId}", photoId);
                return true; // nothing to do
            }

            // 3. Reassemble original file from chunks via IDownloadService
            //    (same pattern as Music module — files are stored as content-addressable
            //    chunks, NOT single blobs, so we must reassemble before processing).
            var caller = new CallerContext(photo.OwnerId, [], CallerType.System);
            using var sourceStream = await _downloadService.DownloadCurrentAsync(
                photo.FileNodeId, caller, cancellationToken);

            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            // 4. Apply each edit operation to the image
            foreach (var record in editRecords)
            {
                if (!Enum.TryParse<PhotoEditType>(record.OperationType, out var editType))
                    continue;

                var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.ParametersJson)
                    ?? new Dictionary<string, string>();

                ApplyEditToImage(image, editType, parameters);
            }

            // 5. Regenerate thumbnails from the edited image
            var gridData = await GenerateThumbnailBytesAsync(image, PhotoThumbnailSize.Grid, cancellationToken);
            var detailData = await GenerateThumbnailBytesAsync(image, PhotoThumbnailSize.Detail, cancellationToken);

            photo.ThumbnailGrid = gridData;
            photo.ThumbnailDetail = detailData;
            photo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SaveEdits: Thumbnails regenerated with {EditCount} edits for photo {PhotoId}",
                editRecords.Count, photoId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveEdits: Failed to save edits for photo {PhotoId}", photoId);
            return false;
        }
    }

    private static void ApplyEditToImage(Image image, PhotoEditType editType, Dictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("value", out var valueStr) || !int.TryParse(valueStr, out var value))
            return;

        image.Mutate(ctx =>
        {
            switch (editType)
            {
                case PhotoEditType.Rotate:
                    ctx.Rotate(value);
                    break;

                case PhotoEditType.Flip:
                    if (value == 0)
                        ctx.Flip(FlipMode.Horizontal);
                    else
                        ctx.Flip(FlipMode.Vertical);
                    break;

                case PhotoEditType.Brightness:
                    // value is -100 to 100; ImageSharp expects a multiplier where 1.0 = no change
                    ctx.Brightness(1.0f + (value / 100f));
                    break;

                case PhotoEditType.Contrast:
                    ctx.Contrast(1.0f + (value / 100f));
                    break;

                case PhotoEditType.Saturation:
                    ctx.Saturate(1.0f + (value / 100f));
                    break;

                case PhotoEditType.Blur:
                    if (value > 0)
                        ctx.GaussianBlur(value);
                    break;

                case PhotoEditType.Sharpen:
                    if (value > 0)
                        ctx.GaussianSharpen(value);
                    break;
            }
        });
    }
}
