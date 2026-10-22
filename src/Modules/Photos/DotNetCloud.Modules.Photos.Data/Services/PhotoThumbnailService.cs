using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PhotoThumbnailService> _logger;
    private IFileStorageEngine? _storageEngine;
    private IDownloadService? _downloadService;
    private IThumbnailService? _filesThumbnailService;
    private readonly int _jpegQuality;
    private readonly int _detailMaxDimension;
    private readonly int _gridMaxDimension;
    private readonly bool _useFilesCachedGridThumbnail;

    /// <summary>
    /// Initializes the photo thumbnail service.
    /// Files module services (IFileStorageEngine, IDownloadService) are resolved lazily —
    /// they are only needed for thumbnail generation, not for serving cached thumbnails.
    /// This avoids a hard DI dependency from the Photos module host on Files module services.
    /// </summary>
    /// <param name="db">Photos database context.</param>
    /// <param name="serviceProvider">Provider used to lazily resolve Files module services.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="configuration">
    /// Optional configuration. Reads <c>Photos:Thumbnails:*</c>; when absent the defaults below apply.
    /// </param>
    public PhotoThumbnailService(
        PhotosDbContext db,
        IServiceProvider serviceProvider,
        ILogger<PhotoThumbnailService> logger,
        IConfiguration? configuration = null)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Thumbnail payload and cost are dominated by the full-resolution resize and the JPEG
        // encoder. Both are configurable so a deployment can trade quality for storage/CPU.
        _jpegQuality = ReadInt(configuration, "Photos:Thumbnails:JpegQuality", 80, 1, 100);
        _detailMaxDimension = ReadInt(
            configuration, "Photos:Thumbnails:DetailMaxDimension", (int)PhotoThumbnailSize.Detail, 256, 4096);
        _gridMaxDimension = ReadInt(
            configuration, "Photos:Thumbnails:GridMaxDimension", (int)PhotoThumbnailSize.Grid, 64, 1024);
        // Reusing a cached Files thumbnail only pays off when the full-resolution decode is
        // skipped entirely. Because the detail size still needs that decode, the extra thumbnail
        // read was measured as neutral-to-slower on the production host, so it is opt-in.
        _useFilesCachedGridThumbnail = string.Equals(
            configuration?["Photos:Thumbnails:UseFilesCachedGridThumbnail"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadInt(IConfiguration? configuration, string key, int defaultValue, int min, int max)
    {
        if (configuration is null || !int.TryParse(configuration[key], out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
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
        string? storagePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedMimeTypes.Contains(mimeType))
        {
            _logger.LogDebug("Unsupported MIME type for photo thumbnail: {MimeType}", mimeType);
            return;
        }

        try
        {
            var photo = await _db.Photos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);

            if (photo is null)
            {
                _logger.LogWarning("Photo {PhotoId} not found for thumbnail generation", photoId);
                return;
            }

            // Rescans and duplicate index events call this for photos that are already done.
            // Generating again costs a full-resolution decode + resize for no benefit.
            if (photo.ThumbnailGrid is { Length: > 0 } && photo.ThumbnailDetail is { Length: > 0 })
            {
                _logger.LogDebug("Photo {PhotoId} already has thumbnails — skipping generation", photoId);
                return;
            }

            // A cached Files thumbnail is a fraction of the original's pixels, so using it as the
            // grid source avoids paying for a second full-resolution resize.
            var filesGridSource = await TryLoadFilesGridThumbnailAsync(photo.FileNodeId, cancellationToken);

            // Resolve Files module services lazily — they may not be registered in the
            // Photos module host (process-isolated) DI container.
            _storageEngine ??= _serviceProvider.GetService<IFileStorageEngine>();
            _downloadService ??= _serviceProvider.GetService<IDownloadService>();

            using var sourceStream = await OpenSourceStreamAsync(photo, storagePath, cancellationToken);
            if (sourceStream is null)
            {
                return;
            }

            using var image = await Image.LoadAsync(sourceStream, cancellationToken);

            // One full-resolution resize produces the detail thumbnail...
            using var detailImage = ResizeImage(image, _detailMaxDimension);
            var detailData = await EncodeJpegAsync(detailImage, cancellationToken);

            // ...and the grid is derived from that result (or from the Files cache) instead of
            // re-resizing the full-resolution original a second time.
            byte[] gridData;
            if (filesGridSource is not null)
            {
                using (filesGridSource)
                using (var gridImage = ResizeImage(filesGridSource, _gridMaxDimension))
                {
                    gridData = await EncodeJpegAsync(gridImage, cancellationToken);
                }
            }
            else
            {
                using var gridImage = ResizeImage(detailImage, _gridMaxDimension);
                gridData = await EncodeJpegAsync(gridImage, cancellationToken);
            }

            photo.ThumbnailGrid = gridData;
            photo.ThumbnailDetail = detailData;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Photo thumbnails generated and stored in DB for {PhotoId} (grid={GridSize}b, detail={DetailSize}b, gridSource={GridSource})",
                photoId, gridData.Length, detailData.Length, filesGridSource is null ? "detail" : "files-cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnails for photo {PhotoId} from storage {Path}", photoId, storagePath);
        }
    }

    /// <summary>
    /// Opens the photo's original bytes: the direct content-addressable blob when available,
    /// otherwise a chunk-reassembled stream from the Files module.
    /// </summary>
    private async Task<Stream?> OpenSourceStreamAsync(
        Photo photo,
        string? storagePath,
        CancellationToken cancellationToken)
    {
        // Try the direct storage path first (fast path for whole-file blobs),
        // then fall back to chunk reconstruction via IDownloadService.
        if (!string.IsNullOrEmpty(storagePath) && _storageEngine is not null)
        {
            var direct = await _storageEngine.OpenReadStreamAsync(storagePath, cancellationToken);
            if (direct is not null)
            {
                return direct;
            }
        }

        if (_downloadService is null)
        {
            _logger.LogWarning(
                "Cannot reconstruct photo {PhotoId}: IDownloadService is not available. " +
                "Ensure Files module services are registered in the host DI container.",
                photo.Id);
            return null;
        }

        try
        {
            var caller = new CallerContext(photo.OwnerId, [], CallerType.System);
            return await _downloadService.DownloadCurrentAsync(photo.FileNodeId, caller, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reconstruct file from chunks for photo {PhotoId} (FileNode {FileNodeId})",
                photo.Id, photo.FileNodeId);
            return null;
        }
    }

    /// <summary>
    /// Returns the Files module's cached large thumbnail (512 px) for the node, if one exists.
    /// This is a cache-only lookup — it never generates a Files thumbnail.
    /// </summary>
    private async Task<Image?> TryLoadFilesGridThumbnailAsync(Guid fileNodeId, CancellationToken cancellationToken)
    {
        if (!_useFilesCachedGridThumbnail)
        {
            return null;
        }

        _filesThumbnailService ??= _serviceProvider.GetService<IThumbnailService>();
        if (_filesThumbnailService is null)
        {
            return null;
        }

        try
        {
            var (data, _) = await _filesThumbnailService.GetThumbnailAsync(
                fileNodeId, DotNetCloud.Modules.Files.Services.ThumbnailSize.Large, cancellationToken);

            if (data is null)
            {
                return null;
            }

            await using (data)
            {
                return await Image.LoadAsync(data, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Files cached thumbnail unavailable for FileNode {FileNodeId}", fileNodeId);
            return null;
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

    private static Image ResizeImage(Image source, int maxDimension)
    {
        if (source.Width <= maxDimension && source.Height <= maxDimension)
        {
            return source.Clone(_ => { });
        }

        return source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(maxDimension, maxDimension),
            Mode = ResizeMode.Max
        }));
    }

    private async Task<byte[]> EncodeJpegAsync(Image image, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = _jpegQuality }, cancellationToken);
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
            _downloadService ??= _serviceProvider.GetService<IDownloadService>();
            if (_downloadService is null)
            {
                _logger.LogWarning("SaveEdits: IDownloadService not available — cannot edit photo {PhotoId}", photoId);
                return false;
            }

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

            // 5. Regenerate thumbnails from the edited image. The grid is derived from the
            //    detail result so only one full-resolution resize is ever performed.
            using var detailImage = ResizeImage(image, _detailMaxDimension);
            var detailData = await EncodeJpegAsync(detailImage, cancellationToken);

            byte[] gridData;
            using (var gridImage = ResizeImage(detailImage, _gridMaxDimension))
            {
                gridData = await EncodeJpegAsync(gridImage, cancellationToken);
            }

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
