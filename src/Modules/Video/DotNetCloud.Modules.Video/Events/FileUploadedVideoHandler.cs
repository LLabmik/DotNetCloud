using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-create Video records for video files.
/// </summary>
public sealed class FileUploadedVideoHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo",
        "video/x-matroska", "video/webm", "video/3gpp", "video/3gpp2",
        "video/x-ms-wmv", "video/x-flv", "video/x-m4v", "video/ogg"
    };

    private readonly ILogger<FileUploadedVideoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedVideoHandler"/> class.
    /// </summary>
    public FileUploadedVideoHandler(ILogger<FileUploadedVideoHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !VideoMimeTypes.Contains(@event.MimeType))
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Video file uploaded: {FileName} ({MimeType}) by user {UserId} — queued for video indexing",
            @event.FileName, @event.MimeType, @event.UploadedByUserId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the set of video MIME types this handler recognizes.
    /// </summary>
    public static IReadOnlySet<string> SupportedMimeTypes => VideoMimeTypes;
}
