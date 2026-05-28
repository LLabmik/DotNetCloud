using DotNetCloud.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Manages album art extraction, caching, and retrieval.
/// Uses content-addressed storage so identical art is stored once regardless of album.
/// </summary>
public sealed class AlbumArtService
{
    private readonly MusicMetadataService _metadataService;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly ILogger<AlbumArtService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumArtService"/> class.
    /// </summary>
    public AlbumArtService(MusicMetadataService metadataService, ContentAddressedStorage contentStorage, ILogger<AlbumArtService> logger)
    {
        _metadataService = metadataService;
        _contentStorage = contentStorage;
        _logger = logger;
    }

    /// <summary>
    /// Extracts and caches album art from an audio file or falls back to folder art.
    /// Returns the content hash of the cached art image.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <returns>The content hash of the cached art, or null if none found.</returns>
    public string? ExtractAndCacheArt(string audioFilePath)
    {
        // Try embedded art first
        var embedded = _metadataService.ExtractEmbeddedArt(audioFilePath);
        if (embedded.HasValue)
        {
            return CacheArtData(embedded.Value.Data, embedded.Value.MimeType);
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
                    return CacheArtData(data, mimeType);
                }
            }
        }

        _logger.LogDebug("No album art found from {AudioFilePath}", audioFilePath);
        return null;
    }

    /// <summary>
    /// Extracts and caches album art from an audio stream (reassembled from chunks).
    /// Returns the content hash of the cached art image.
    /// </summary>
    /// <param name="audioStream">Seekable stream containing the complete audio file.</param>
    /// <param name="mimeType">Audio MIME type (e.g. "audio/mpeg").</param>
    /// <param name="fileName">Display file name for TagLib abstraction.</param>
    /// <returns>The content hash of the cached art, or null if none found.</returns>
    public string? ExtractAndCacheArt(Stream audioStream, string mimeType, string fileName)
    {
        var embedded = _metadataService.ExtractEmbeddedArt(audioStream, mimeType, fileName);
        if (embedded.HasValue)
        {
            return CacheArtData(embedded.Value.Data, embedded.Value.MimeType);
        }

        _logger.LogDebug("No album art found in stream");
        return null;
    }

    /// <summary>
    /// Returns the content hash for an existing cached art item. With content-addressed
    /// storage, no copy is needed — the same hash works for any album referencing the same art.
    /// </summary>
    /// <param name="contentHash">Existing content hash of the source art.</param>
    /// <returns>The same content hash (no copy needed).</returns>
    public string? CopyArtFromExisting(string? contentHash)
    {
        return contentHash; // Content-addressed: no copy needed, same hash works for all
    }

    private string? CacheArtData(byte[] data, string mimeType)
    {
        try
        {
            var extension = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var hash = _contentStorage.Store(data, extension);
            _logger.LogDebug("Cached album art with content hash {Hash}", hash);
            return hash;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache album art");
            return null;
        }
    }
}
