using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-index Track records for audio files.
/// Delegates to LibraryScanService in the Data layer via callback interface.
/// </summary>
public sealed class FileUploadedMusicHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> AudioMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg", "audio/mp3", "audio/flac", "audio/ogg",
        "audio/vorbis", "audio/opus", "audio/aac", "audio/mp4",
        "audio/m4a", "audio/x-m4a", "audio/wav", "audio/x-wav",
        "audio/wave", "audio/x-ms-wma", "audio/webm"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".oga", ".opus", ".aac",
        ".m4a", ".wav", ".wma", ".aiff", ".aif", ".wv", ".ape", ".webm"
    };

    private readonly IMusicIndexingCallback? _indexingCallback;
    private readonly ILogger<FileUploadedMusicHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedMusicHandler"/> class.
    /// </summary>
    public FileUploadedMusicHandler(ILogger<FileUploadedMusicHandler> logger, IMusicIndexingCallback? indexingCallback = null)
    {
        _logger = logger;
        _indexingCallback = indexingCallback;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        var isAudioMime = !string.IsNullOrEmpty(@event.MimeType) && AudioMimeTypes.Contains(@event.MimeType);
        var isAudioExt = AudioExtensions.Contains(Path.GetExtension(@event.FileName));
        if (!isAudioMime && !isAudioExt)
        {
            return;
        }

        if (_indexingCallback is not null)
        {
            try
            {
                // Use the event MIME type, or guess from extension if null
                var effectiveMime = @event.MimeType ?? GuessMimeFromExtension(@event.FileName) ?? "application/octet-stream";
                await _indexingCallback.IndexAudioAsync(
                    @event.FileNodeId, @event.FileName, effectiveMime, @event.Size,
                    @event.UploadedByUserId, @event.StoragePath, cancellationToken);

                _logger.LogInformation(
                    "Track auto-indexed for uploaded audio: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-index track for uploaded audio: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Audio file uploaded: {FileName} ({MimeType}) by user {UserId} — indexing callback not registered",
                @event.FileName, @event.MimeType, @event.UploadedByUserId);
        }
    }

    /// <summary>
    /// Gets the set of audio MIME types this handler recognizes.
    /// </summary>
    public static IReadOnlySet<string> SupportedMimeTypes => AudioMimeTypes;

    private static string? GuessMimeFromExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return null;
        return ext.ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" or ".oga" => "audio/ogg",
            ".opus" => "audio/opus",
            ".aac" => "audio/aac",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".aiff" or ".aif" => "audio/aiff",
            ".wv" => "audio/wavpack",
            ".ape" => "audio/ape",
            ".webm" => "audio/webm",
            _ => null,
        };
    }
}

/// <summary>
/// Callback interface for music indexing — implemented in the Data layer, injected via DI.
/// Avoids direct dependency from Module → Data layer.
/// </summary>
public interface IMusicIndexingCallback
{
    /// <summary>Indexes an uploaded audio file into the music library.</summary>
    /// <param name="fileNodeId">The Files-module node ID.</param>
    /// <param name="fileName">Display file name.</param>
    /// <param name="mimeType">MIME type of the audio file.</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="storagePath">Relative content-addressable storage path (for metadata extraction). Null when unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAudioAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of FileNode IDs that are already indexed in the music library for the given owner.
    /// Used by the scanner to skip files that have not changed since last scan.
    /// </summary>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes Track records whose source FileNodes no longer exist in the Files module.
    /// Also removes orphaned albums and artists (those with zero remaining non-deleted tracks).
    /// </summary>
    /// <param name="deletedFileNodeIds">FileNode IDs whose backing files have been deleted.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tracks removed.</returns>
    Task<int> RemoveDeletedTracksAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all music library metadata from the database (tracks, albums, artists, etc.).
    /// The actual audio files are NOT affected.
    /// </summary>
    Task ResetCollectionAsync(CancellationToken cancellationToken = default);
}
