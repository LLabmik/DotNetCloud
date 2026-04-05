using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Implements the music indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedMusicHandler when an audio file is uploaded.
/// </summary>
public sealed class MusicIndexingCallback : IMusicIndexingCallback
{
    private readonly LibraryScanService _libraryScanService;
    private readonly ILogger<MusicIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicIndexingCallback"/> class.
    /// </summary>
    public MusicIndexingCallback(LibraryScanService libraryScanService, ILogger<MusicIndexingCallback> logger)
    {
        _libraryScanService = libraryScanService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexAudioAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CancellationToken cancellationToken = default)
    {
        await _libraryScanService.IndexFileAsync(
            fileNodeId, fileName, mimeType, sizeBytes, ownerId,
            artCacheDir: null, cancellationToken);

        _logger.LogDebug("Audio indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }
}
