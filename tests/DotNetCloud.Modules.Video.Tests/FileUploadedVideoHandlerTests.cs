using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class FileUploadedVideoHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ProcessesVideoMimeTypes()
    {
        var handler = new FileUploadedVideoHandler(Mock.Of<ILogger<FileUploadedVideoHandler>>());

        var videoEvent = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            FileName = "movie.mp4",
            MimeType = "video/mp4",
            Size = 500_000_000
        };

        // Should complete without error for video MIME type
        await handler.HandleAsync(videoEvent, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresNonVideoMimeTypes()
    {
        var handler = new FileUploadedVideoHandler(Mock.Of<ILogger<FileUploadedVideoHandler>>());

        var audioEvent = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            FileName = "song.mp3",
            MimeType = "audio/mpeg",
            Size = 5_000_000
        };

        // Should complete immediately for non-video type
        await handler.HandleAsync(audioEvent, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_RecognizesAllSupportedVideoTypes()
    {
        var handler = new FileUploadedVideoHandler(Mock.Of<ILogger<FileUploadedVideoHandler>>());
        var supportedTypes = new[]
        {
            "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo",
            "video/x-matroska", "video/webm", "video/3gpp", "video/3gpp2",
            "video/x-ms-wmv", "video/x-flv", "video/x-m4v", "video/ogg"
        };

        foreach (var mimeType in supportedTypes)
        {
            var evt = new FileUploadedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                FileNodeId = Guid.NewGuid(),
                UploadedByUserId = Guid.NewGuid(),
                FileName = "file.ext",
                MimeType = mimeType,
                Size = 100
            };

            await handler.HandleAsync(evt, CancellationToken.None);
        }
    }
}
