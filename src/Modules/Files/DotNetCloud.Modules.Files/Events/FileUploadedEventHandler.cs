using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Handles <see cref="FileUploadedEvent"/> by logging the upload.
/// Modules can subscribe additional handlers for notifications, indexing, etc.
/// </summary>
public sealed class FileUploadedEventHandler : IEventHandler<FileUploadedEvent>
{
    private readonly ILogger<FileUploadedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedEventHandler"/> class.
    /// </summary>
    public FileUploadedEventHandler(ILogger<FileUploadedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "File uploaded: {FileName} ({Size} bytes) by user {UserId}",
            @event.FileName,
            @event.Size,
            @event.UploadedByUserId);

        return Task.CompletedTask;
    }
}
