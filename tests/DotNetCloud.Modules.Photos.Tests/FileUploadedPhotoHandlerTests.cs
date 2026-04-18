using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public class FileUploadedPhotoHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ImageMimeType_CallsIndexingCallback()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.jpg", "image/jpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            evt.FileNodeId, evt.FileName, evt.MimeType!, evt.Size,
            evt.UploadedByUserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NonImageMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.mp3", "audio/mpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NullMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", null);
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_EmptyMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", "");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NoCallback_CompletesWithoutError()
    {
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            indexingCallback: null);

        var evt = CreateEvent("photo.jpg", "image/jpeg");
        await handler.HandleAsync(evt, CancellationToken.None);
        // Should not throw
    }

    [TestMethod]
    public async Task HandleAsync_CallbackThrows_DoesNotPropagate()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        callbackMock.Setup(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.jpg", "image/jpeg");
        await handler.HandleAsync(evt, CancellationToken.None);
        // Should not throw — error is caught and logged
    }

    [TestMethod]
    public async Task HandleAsync_RecognizesAllSupportedImageTypes()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var supportedTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "image/bmp", "image/tiff", "image/svg+xml", "image/heic", "image/heif"
        };

        foreach (var mimeType in supportedTypes)
        {
            var evt = CreateEvent("file.ext", mimeType);
            await handler.HandleAsync(evt, CancellationToken.None);
        }

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(supportedTypes.Length));
    }

    [TestMethod]
    public async Task HandleAsync_CaseInsensitiveMimeType_StillMatches()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.jpg", "Image/JPEG");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresVideoMimeTypes()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("video.mp4", "video/mp4");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresAudioMimeTypes()
    {
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.flac", "audio/flac");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void SupportedMimeTypes_ContainsExpectedTypes()
    {
        var types = FileUploadedPhotoHandler.SupportedMimeTypes;
        Assert.IsTrue(types.Contains("image/jpeg"));
        Assert.IsTrue(types.Contains("image/png"));
        Assert.IsTrue(types.Contains("image/gif"));
        Assert.IsTrue(types.Contains("image/webp"));
        Assert.IsTrue(types.Contains("image/heic"));
        Assert.IsTrue(types.Contains("image/heif"));
        Assert.AreEqual(9, types.Count);
    }

    [TestMethod]
    public async Task HandleAsync_PassesCorrectParametersToCallback()
    {
        var fileNodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callbackMock = new Mock<IPhotoIndexingCallback>();
        var handler = new FileUploadedPhotoHandler(
            Mock.Of<ILogger<FileUploadedPhotoHandler>>(),
            callbackMock.Object);

        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            UploadedByUserId = userId,
            FileName = "vacation.png",
            MimeType = "image/png",
            Size = 2_500_000
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexPhotoAsync(
            fileNodeId, "vacation.png", "image/png", 2_500_000, userId,
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
