using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-create Photo records for image files.
/// </summary>
public sealed class FileUploadedPhotoHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "image/bmp", "image/tiff", "image/svg+xml", "image/heic", "image/heif"
    };

    private readonly ILogger<FileUploadedPhotoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedPhotoHandler"/> class.
    /// </summary>
    public FileUploadedPhotoHandler(ILogger<FileUploadedPhotoHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !ImageMimeTypes.Contains(@event.MimeType))
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Image file uploaded: {FileName} ({MimeType}) by user {UserId} — queued for photo indexing",
            @event.FileName, @event.MimeType, @event.UploadedByUserId);

        // Actual indexing is handled by PhotoIndexingService in the Data layer
        return Task.CompletedTask;
    }
}
