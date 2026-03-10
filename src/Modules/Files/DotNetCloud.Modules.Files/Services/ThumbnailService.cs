using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Generates JPEG thumbnails from raster images, video first frames, and PDF first pages,
/// then caches them on disk.
/// Thumbnails are stored in a two-level directory structure under the storage root to avoid
/// large flat directories (e.g. <c>.thumbnails/ab/abcdef01..._{size}.jpg</c>).
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private static readonly HashSet<string> _supportedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff"
    };

    private static readonly HashSet<string> _supportedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/mpeg",
        "video/quicktime",
        "video/x-msvideo",
        "video/x-matroska",
        "video/webm"
    };

    private readonly string _cacheRoot;
    private readonly IVideoFrameExtractor _videoFrameExtractor;
    private readonly IPdfPageRenderer _pdfPageRenderer;
    private readonly ILogger<ThumbnailService> _logger;

    /// <summary>
    /// Initialises the thumbnail service, resolving the cache root from configuration.
    /// </summary>
    public ThumbnailService(
        IConfiguration configuration,
        IVideoFrameExtractor videoFrameExtractor,
        IPdfPageRenderer pdfPageRenderer,
        ILogger<ThumbnailService> logger)
    {
        _videoFrameExtractor = videoFrameExtractor;
        _pdfPageRenderer = pdfPageRenderer;
        _logger = logger;
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _cacheRoot = Path.Combine(storageRoot, ".thumbnails");
        Directory.CreateDirectory(_cacheRoot);
    }

    /// <inheritdoc />
    public Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid fileNodeId,
        ThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        var path = BuildCachePath(fileNodeId, size);
        if (!File.Exists(path))
            return Task.FromResult<(Stream?, string?)>((null, null));

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<(Stream?, string?)>((stream, "image/jpeg"));
    }

    /// <inheritdoc />
    public async Task GenerateThumbnailAsync(
        Guid fileNodeId,
        string storagePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storagePath))
        {
            _logger.LogWarning("Cannot generate thumbnail — source file not found: {Path}", storagePath);
            return;
        }

        if (_supportedImageMimeTypes.Contains(mimeType))
        {
            await GenerateFromImageAsync(fileNodeId, storagePath, cancellationToken);
            return;
        }

        if (_supportedVideoMimeTypes.Contains(mimeType))
        {
            await GenerateFromVideoAsync(fileNodeId, storagePath, cancellationToken);
            return;
        }

        if (string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            await GenerateFromPdfAsync(fileNodeId, storagePath, cancellationToken);
        }
    }

    private async Task GenerateFromImageAsync(Guid fileNodeId, string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync(storagePath, cancellationToken);
            await GenerateResizedThumbnailsAsync(fileNodeId, image, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for file {FileId}", fileNodeId);
        }
    }

    private async Task GenerateFromVideoAsync(Guid fileNodeId, string storagePath, CancellationToken cancellationToken)
    {
        var tempFramePath = Path.Combine(Path.GetTempPath(), $"dnc-thumb-{Guid.NewGuid():N}.jpg");

        try
        {
            var extracted = await _videoFrameExtractor.TryExtractFrameAsync(storagePath, tempFramePath, cancellationToken);
            if (!extracted || !File.Exists(tempFramePath))
            {
                _logger.LogWarning("Failed to extract video frame for thumbnail generation: {Path}", storagePath);
                return;
            }

            using var image = await Image.LoadAsync(tempFramePath, cancellationToken);
            await GenerateResizedThumbnailsAsync(fileNodeId, image, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate video thumbnail for file {FileId}", fileNodeId);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFramePath))
                {
                    File.Delete(tempFramePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not clean up temporary video frame: {Path}", tempFramePath);
            }
        }
    }

    private async Task GenerateFromPdfAsync(Guid fileNodeId, string storagePath, CancellationToken cancellationToken)
    {
        var tempPageImagePath = Path.Combine(Path.GetTempPath(), $"dnc-thumb-pdf-{Guid.NewGuid():N}.jpg");

        try
        {
            var rendered = await _pdfPageRenderer.TryRenderFirstPageAsync(storagePath, tempPageImagePath, cancellationToken);
            if (!rendered || !File.Exists(tempPageImagePath))
            {
                _logger.LogWarning("Failed to render PDF first page for thumbnail generation: {Path}", storagePath);
                return;
            }

            using var image = await Image.LoadAsync(tempPageImagePath, cancellationToken);
            await GenerateResizedThumbnailsAsync(fileNodeId, image, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF thumbnail for file {FileId}", fileNodeId);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPageImagePath))
                {
                    File.Delete(tempPageImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not clean up temporary PDF render image: {Path}", tempPageImagePath);
            }
        }
    }

    private async Task GenerateResizedThumbnailsAsync(Guid fileNodeId, Image image, CancellationToken cancellationToken)
    {
        foreach (ThumbnailSize size in Enum.GetValues<ThumbnailSize>())
        {
            var outputPath = BuildCachePath(fileNodeId, size);
            if (File.Exists(outputPath))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var dim = (int)size;
            using var thumb = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(dim, dim),
                Mode = ResizeMode.Max
            }));

            await thumb.SaveAsJpegAsync(outputPath, cancellationToken);
            _logger.LogDebug("Generated {Size}px thumbnail for file {FileId}", dim, fileNodeId);
        }
    }

    /// <inheritdoc />
    public Task DeleteThumbnailsAsync(Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        foreach (ThumbnailSize size in Enum.GetValues<ThumbnailSize>())
        {
            var path = BuildCachePath(fileNodeId, size);
            if (!File.Exists(path))
                continue;

            try
            {
                File.Delete(path);
                _logger.LogDebug("Deleted {Size}px thumbnail for file {FileId}", (int)size, fileNodeId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete thumbnail at {Path}", path);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the cache file path for a specific node and size, using a two-char hex prefix
    /// subdirectory to avoid oversized flat directories.
    /// </summary>
    private string BuildCachePath(Guid fileNodeId, ThumbnailSize size)
    {
        var id = fileNodeId.ToString("N");
        var prefix = id[..2];
        return Path.Combine(_cacheRoot, prefix, $"{id}_{(int)size}.jpg");
    }
}
