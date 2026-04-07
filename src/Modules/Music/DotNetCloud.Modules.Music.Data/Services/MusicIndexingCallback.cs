using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Implements the music indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedMusicHandler when an audio file is uploaded.
/// </summary>
public sealed class MusicIndexingCallback : IMusicIndexingCallback
{
    private readonly LibraryScanService _libraryScanService;
    private readonly string _storageRootPath;
    private readonly ILogger<MusicIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicIndexingCallback"/> class.
    /// </summary>
    public MusicIndexingCallback(LibraryScanService libraryScanService, IConfiguration configuration, ILogger<MusicIndexingCallback> logger)
    {
        _libraryScanService = libraryScanService;
        _storageRootPath = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexAudioAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        // Resolve absolute file path for TagLib metadata extraction.
        // storagePath is the relative content-addressable path (e.g. "chunks/ab/cd/abcdef…").
        // If available, combine with storage root to get the on-disk path.
        string metadataFilePath = fileName;
        if (!string.IsNullOrEmpty(storagePath))
        {
            var absolutePath = Path.Combine(_storageRootPath, storagePath.Replace('\\', '/'));
            if (File.Exists(absolutePath))
            {
                metadataFilePath = absolutePath;
            }
            else
            {
                _logger.LogWarning("Storage path {StoragePath} resolved to {AbsolutePath} but file not found, using filename for fallback metadata", storagePath, absolutePath);
            }
        }

        await _libraryScanService.IndexFileAsync(
            fileNodeId, fileName, mimeType, sizeBytes, ownerId,
            metadataFilePath: metadataFilePath, artCacheDir: null, cancellationToken);

        _logger.LogDebug("Audio indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }
}
