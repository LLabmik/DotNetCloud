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
        if (string.IsNullOrEmpty(@event.MimeType) || !AudioMimeTypes.Contains(@event.MimeType))
        {
            return;
        }

        if (_indexingCallback is not null)
        {
            try
            {
                await _indexingCallback.IndexAudioAsync(
                    @event.FileNodeId, @event.FileName, @event.MimeType, @event.Size,
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
}
