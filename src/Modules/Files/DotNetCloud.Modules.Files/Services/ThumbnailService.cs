using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Generates JPEG thumbnails from raster image files using ImageSharp and caches them on disk.
/// Thumbnails are stored in a two-level directory structure under the storage root to avoid
/// large flat directories (e.g. <c>.thumbnails/ab/abcdef01..._{size}.jpg</c>).
/// </summary>
public sealed class ThumbnailService : IThumbnailService
{
    private static readonly HashSet<string> _supportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff"
    };

    private readonly string _cacheRoot;
    private readonly ILogger<ThumbnailService> _logger;

    /// <summary>
    /// Initialises the thumbnail service, resolving the cache root from configuration.
    /// </summary>
    public ThumbnailService(IConfiguration configuration, ILogger<ThumbnailService> logger)
    {
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
        if (!_supportedMimeTypes.Contains(mimeType))
            return;

        if (!File.Exists(storagePath))
        {
            _logger.LogWarning("Cannot generate thumbnail — source file not found: {Path}", storagePath);
            return;
        }

        try
        {
            using var image = await Image.LoadAsync(storagePath, cancellationToken);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for file {FileId}", fileNodeId);
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
