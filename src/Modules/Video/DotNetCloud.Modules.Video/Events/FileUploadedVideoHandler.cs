using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-create Video records for video files.
/// Delegates to VideoService in the Data layer via callback interface.
/// </summary>
public sealed class FileUploadedVideoHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo",
        "video/x-matroska", "video/webm", "video/3gpp", "video/3gpp2",
        "video/x-ms-wmv", "video/x-flv", "video/x-m4v", "video/ogg"
    };

    private readonly IVideoIndexingCallback? _indexingCallback;
    private readonly ILogger<FileUploadedVideoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedVideoHandler"/> class.
    /// </summary>
    public FileUploadedVideoHandler(ILogger<FileUploadedVideoHandler> logger, IVideoIndexingCallback? indexingCallback = null)
    {
        _logger = logger;
        _indexingCallback = indexingCallback;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !VideoMimeTypes.Contains(@event.MimeType))
        {
            return;
        }

        if (_indexingCallback is not null)
        {
            try
            {
                await _indexingCallback.IndexVideoAsync(
                    @event.FileNodeId, @event.FileName, @event.MimeType, @event.Size,
                    @event.UploadedByUserId, @event.StoragePath, cancellationToken);

                _logger.LogInformation(
                    "Video auto-created for uploaded file: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-create video for uploaded file: {FileName} ({MimeType}) by user {UserId}",
                    @event.FileName, @event.MimeType, @event.UploadedByUserId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Video file uploaded: {FileName} ({MimeType}) by user {UserId} — indexing callback not registered",
                @event.FileName, @event.MimeType, @event.UploadedByUserId);
        }
    }

    /// <summary>
    /// Gets the set of video MIME types this handler recognizes.
    /// </summary>
    public static IReadOnlySet<string> SupportedMimeTypes => VideoMimeTypes;
}

/// <summary>
/// Callback interface for video indexing — implemented in the Data layer, injected via DI.
/// Avoids direct dependency from Module → Data layer.
/// </summary>
public interface IVideoIndexingCallback
{
    /// <summary>Indexes an uploaded video file as a Video record.</summary>
    /// <param name="fileNodeId">The Files-module node ID.</param>
    /// <param name="fileName">Display file name.</param>
    /// <param name="mimeType">MIME type of the video file.</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="ownerId">Owner user ID.</param>
    /// <param name="storagePath">Relative content-addressable storage path. Null when unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all video library metadata from the database (videos, collections, subtitles,
    /// watch progress, watch history, shares). The actual video files are NOT affected.
    /// After calling this, a re-scan will rebuild the entire library from scratch.
    /// </summary>
    Task ResetCollectionAsync(CancellationToken cancellationToken = default);
}
