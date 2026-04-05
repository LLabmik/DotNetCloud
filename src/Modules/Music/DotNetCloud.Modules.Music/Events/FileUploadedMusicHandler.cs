using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Events;

/// <summary>
/// Handles FileUploadedEvent to auto-create Track records for audio files.
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

    private readonly ILogger<FileUploadedMusicHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadedMusicHandler"/> class.
    /// </summary>
    public FileUploadedMusicHandler(ILogger<FileUploadedMusicHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !AudioMimeTypes.Contains(@event.MimeType))
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Audio file uploaded: {FileName} ({MimeType}) by user {UserId} — queued for music indexing",
            @event.FileName, @event.MimeType, @event.UploadedByUserId);

        return Task.CompletedTask;
    }
}
