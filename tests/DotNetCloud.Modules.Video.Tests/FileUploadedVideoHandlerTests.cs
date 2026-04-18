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
    public async Task HandleAsync_VideoMimeType_CallsIndexingCallback()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("movie.mp4", "video/mp4");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            evt.FileNodeId, evt.FileName, evt.MimeType!, evt.Size,
            evt.UploadedByUserId, evt.StoragePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NonVideoMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.mp3", "audio/mpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NullMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", null);
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_EmptyMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", "");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NoCallback_CompletesWithoutError()
    {
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            indexingCallback: null);

        var evt = CreateEvent("movie.mp4", "video/mp4");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_CallbackThrows_DoesNotPropagate()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        callbackMock.Setup(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("movie.mp4", "video/mp4");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_RecognizesAllSupportedVideoTypes()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var supportedTypes = new[]
        {
            "video/mp4", "video/mpeg", "video/quicktime", "video/x-msvideo",
            "video/x-matroska", "video/webm", "video/3gpp", "video/3gpp2",
            "video/x-ms-wmv", "video/x-flv", "video/x-m4v", "video/ogg"
        };

        foreach (var mimeType in supportedTypes)
        {
            var evt = CreateEvent("file.ext", mimeType);
            await handler.HandleAsync(evt, CancellationToken.None);
        }

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(supportedTypes.Length));
    }

    [TestMethod]
    public async Task HandleAsync_CaseInsensitiveMimeType_StillMatches()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("movie.mp4", "Video/MP4");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresImageMimeTypes()
    {
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.jpg", "image/jpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void SupportedMimeTypes_ContainsExpectedTypes()
    {
        var types = FileUploadedVideoHandler.SupportedMimeTypes;
        Assert.IsTrue(types.Contains("video/mp4"));
        Assert.IsTrue(types.Contains("video/x-matroska"));
        Assert.IsTrue(types.Contains("video/webm"));
        Assert.AreEqual(12, types.Count);
    }

    [TestMethod]
    public async Task HandleAsync_PassesCorrectParametersToCallback()
    {
        var fileNodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callbackMock = new Mock<IVideoIndexingCallback>();
        var handler = new FileUploadedVideoHandler(
            Mock.Of<ILogger<FileUploadedVideoHandler>>(),
            callbackMock.Object);

        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            UploadedByUserId = userId,
            FileName = "documentary.mkv",
            MimeType = "video/x-matroska",
            Size = 1_500_000_000
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexVideoAsync(
            fileNodeId, "documentary.mkv", "video/x-matroska", 1_500_000_000, userId,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FileUploadedEvent CreateEvent(string fileName, string? mimeType) => new()
    {
        EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        FileNodeId = Guid.NewGuid(),
        UploadedByUserId = Guid.NewGuid(),
        FileName = fileName,
        MimeType = mimeType,
        Size = 1024
    };
}
