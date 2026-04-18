using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Manages album art extraction, caching, and retrieval.
/// </summary>
public sealed class AlbumArtService
{
    private readonly MusicMetadataService _metadataService;
    private readonly ILogger<AlbumArtService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumArtService"/> class.
    /// </summary>
    public AlbumArtService(MusicMetadataService metadataService, ILogger<AlbumArtService> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <summary>
    /// Extracts and caches album art from an audio file or falls back to folder art.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <param name="cacheDir">Directory where cached art is stored.</param>
    /// <param name="albumId">Album ID for the cache filename.</param>
    /// <returns>The relative cache path to the art file, or null if none found.</returns>
    public string? ExtractAndCacheArt(string audioFilePath, string cacheDir, Guid albumId)
    {
        // Try embedded art first
        var embedded = _metadataService.ExtractEmbeddedArt(audioFilePath);
        if (embedded.HasValue)
        {
            return CacheArtData(embedded.Value.Data, embedded.Value.MimeType, cacheDir, albumId);
        }

        // Fall back to folder art
        var directory = Path.GetDirectoryName(audioFilePath);
        if (directory is not null)
        {
            var folderArtNames = new[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png", "album.jpg", "album.png" };
            foreach (var artName in folderArtNames)
            {
                var artPath = Path.Combine(directory, artName);
                if (File.Exists(artPath))
                {
                    var data = File.ReadAllBytes(artPath);
                    var mimeType = artName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                    return CacheArtData(data, mimeType, cacheDir, albumId);
                }
            }
        }

        _logger.LogDebug("No album art found for album {AlbumId} from {AudioFilePath}", albumId, audioFilePath);
        return null;
    }

    /// <summary>
    /// Extracts and caches album art from an audio stream (reassembled from chunks).
    /// </summary>
    /// <param name="audioStream">Seekable stream containing the complete audio file.</param>
    /// <param name="mimeType">Audio MIME type (e.g. "audio/mpeg").</param>
    /// <param name="fileName">Display file name for TagLib abstraction.</param>
    /// <param name="cacheDir">Directory where cached art is stored.</param>
    /// <param name="albumId">Album ID for the cache filename.</param>
    /// <returns>The relative cache path to the art file, or null if none found.</returns>
    public string? ExtractAndCacheArt(Stream audioStream, string mimeType, string fileName, string cacheDir, Guid albumId)
    {
        var embedded = _metadataService.ExtractEmbeddedArt(audioStream, mimeType, fileName);
        if (embedded.HasValue)
        {
            return CacheArtData(embedded.Value.Data, embedded.Value.MimeType, cacheDir, albumId);
        }

        _logger.LogDebug("No album art found in stream for album {AlbumId}", albumId);
        return null;
    }

    /// <summary>
    /// Gets the cached art path for an album if it exists.
    /// </summary>
    public string? GetCachedArtPath(string cacheDir, Guid albumId)
    {
        var jpgPath = Path.Combine(cacheDir, $"{albumId}.jpg");
        if (File.Exists(jpgPath)) return jpgPath;

        var pngPath = Path.Combine(cacheDir, $"{albumId}.png");
        if (File.Exists(pngPath)) return pngPath;

        return null;
    }

    private string? CacheArtData(byte[] data, string mimeType, string cacheDir, Guid albumId)
    {
        try
        {
            Directory.CreateDirectory(cacheDir);
            var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var fileName = $"{albumId}{extension}";
            var cachePath = Path.Combine(cacheDir, fileName);
            File.WriteAllBytes(cachePath, data);
            _logger.LogDebug("Cached album art for {AlbumId} at {Path}", albumId, cachePath);
            return cachePath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache album art for {AlbumId}", albumId);
            return null;
        }
    }
}
