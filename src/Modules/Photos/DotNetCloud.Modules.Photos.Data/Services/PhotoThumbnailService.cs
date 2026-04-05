using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Generates and caches photo-specific thumbnails (grid 300px, detail 1200px)
/// using ImageSharp. Full-size requests return the original image.
/// Thumbnails are cached in a two-level directory structure under the storage root
/// (e.g. <c>.photo-thumbnails/ab/abcdef01..._{size}.jpg</c>).
/// </summary>
public sealed class PhotoThumbnailService : IPhotoThumbnailService
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif",
        "image/webp", "image/bmp", "image/tiff"
    };

    private readonly PhotosDbContext _db;
    private readonly string _cacheRoot;
    private readonly ILogger<PhotoThumbnailService> _logger;

    /// <summary>
    /// Initializes the photo thumbnail service, resolving the cache root from configuration.
    /// </summary>
    public PhotoThumbnailService(
        PhotosDbContext db,
        IConfiguration configuration,
        ILogger<PhotoThumbnailService> logger)
    {
        _db = db;
        _logger = logger;
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _cacheRoot = Path.Combine(storageRoot, ".photo-thumbnails");
        Directory.CreateDirectory(_cacheRoot);
    }

    /// <inheritdoc />
    public Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid photoId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        if (size == PhotoThumbnailSize.Full)
        {
            // Full size is not cached — caller should use the original file via Files module
            return Task.FromResult<(Stream?, string?)>((null, null));
        }

        var path = BuildCachePath(photoId, size);
        if (!File.Exists(path))
        {
            return Task.FromResult<(Stream?, string?)>((null, null));
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<(Stream?, string?)>((stream, "image/jpeg"));
    }

    /// <inheritdoc />
    public async Task GenerateThumbnailsAsync(
        Guid photoId,
        string storagePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storagePath))
        {
            _logger.LogWarning("Source file not found for photo thumbnail generation: {Path}", storagePath);
            return;
        }

        if (!SupportedMimeTypes.Contains(mimeType))
        {
            _logger.LogDebug("Unsupported MIME type for photo thumbnail: {MimeType}", mimeType);
            return;
        }

        try
        {
            using var image = await Image.LoadAsync(storagePath, cancellationToken);

            // Generate grid thumbnail (300px)
            await GenerateSingleThumbnailAsync(image, photoId, PhotoThumbnailSize.Grid, cancellationToken);

            // Generate detail thumbnail (1200px)
            await GenerateSingleThumbnailAsync(image, photoId, PhotoThumbnailSize.Detail, cancellationToken);

            _logger.LogDebug("Photo thumbnails generated for {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnails for photo {PhotoId} from {Path}", photoId, storagePath);
        }
    }

    /// <inheritdoc />
    public Task DeleteThumbnailsAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var sizes = new[] { PhotoThumbnailSize.Grid, PhotoThumbnailSize.Detail };
        foreach (var size in sizes)
        {
            var path = BuildCachePath(photoId, size);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to delete thumbnail {Path}", path);
                }
            }
        }

        _logger.LogDebug("Photo thumbnails deleted for {PhotoId}", photoId);
        return Task.CompletedTask;
    }

    private async Task GenerateSingleThumbnailAsync(
        Image source,
        Guid photoId,
        PhotoThumbnailSize size,
        CancellationToken cancellationToken)
    {
        var maxDimension = (int)size;
        if (maxDimension <= 0)
        {
            return;
        }

        var outputPath = BuildCachePath(photoId, size);
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);

        using var clone = source.Clone(ctx =>
        {
            // Resize maintaining aspect ratio, fitting within maxDimension × maxDimension
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max
            });
        });

        await clone.SaveAsJpegAsync(outputPath, cancellationToken);
    }

    private string BuildCachePath(Guid photoId, PhotoThumbnailSize size)
    {
        var hex = photoId.ToString("N");
        var prefix = hex[..2];
        return Path.Combine(_cacheRoot, prefix, $"{hex}_{(int)size}.jpg");
    }
}
