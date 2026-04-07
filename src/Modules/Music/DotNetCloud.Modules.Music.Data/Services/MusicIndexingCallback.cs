using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
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
    private readonly IDownloadService _downloadService;
    private readonly ILogger<MusicIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicIndexingCallback"/> class.
    /// </summary>
    public MusicIndexingCallback(LibraryScanService libraryScanService, IDownloadService downloadService, ILogger<MusicIndexingCallback> logger)
    {
        _libraryScanService = libraryScanService;
        _downloadService = downloadService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexAudioAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default)
    {
        // Use IDownloadService to reassemble all chunks into a single seekable stream.
        // Files are stored as content-addressable chunks; individual chunks are NOT
        // complete audio files, so we must reassemble before TagLib can parse metadata.
        Stream? audioStream = null;
        try
        {
            var caller = new CallerContext(ownerId, [], CallerType.System);
            audioStream = await _downloadService.DownloadCurrentAsync(fileNodeId, caller, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download file stream for {FileNodeId}, metadata extraction will use fallback", fileNodeId);
        }

        try
        {
            await _libraryScanService.IndexFileAsync(
                fileNodeId, fileName, mimeType, sizeBytes, ownerId,
                audioStream: audioStream, artCacheDir: null, cancellationToken: cancellationToken);
        }
        finally
        {
            if (audioStream is not null)
                await audioStream.DisposeAsync();
        }

        _logger.LogDebug("Audio indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }

    /// <inheritdoc />
    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        await _libraryScanService.ResetCollectionAsync(cancellationToken);
    }
}
