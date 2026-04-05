using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class FileUploadedMusicHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_AudioMimeType_CallsIndexingCallback()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.mp3", "audio/mpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            evt.FileNodeId, evt.FileName, evt.MimeType!, evt.Size,
            evt.UploadedByUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_NonAudioMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.jpg", "image/jpeg");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NullMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", null);
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_EmptyMimeType_DoesNotCallCallback()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("file.bin", "");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_NoCallback_CompletesWithoutError()
    {
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            indexingCallback: null);

        var evt = CreateEvent("song.flac", "audio/flac");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_CallbackThrows_DoesNotPropagate()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        callbackMock.Setup(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.flac", "audio/flac");
        await handler.HandleAsync(evt, CancellationToken.None);
    }

    [TestMethod]
    public async Task HandleAsync_RecognizesAllSupportedAudioTypes()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var supportedTypes = new[]
        {
            "audio/mpeg", "audio/mp3", "audio/flac", "audio/ogg",
            "audio/vorbis", "audio/opus", "audio/aac", "audio/mp4",
            "audio/m4a", "audio/x-m4a", "audio/wav", "audio/x-wav",
            "audio/wave", "audio/x-ms-wma", "audio/webm"
        };

        foreach (var mimeType in supportedTypes)
        {
            var evt = CreateEvent("file.ext", mimeType);
            await handler.HandleAsync(evt, CancellationToken.None);
        }

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(supportedTypes.Length));
    }

    [TestMethod]
    public async Task HandleAsync_CaseInsensitiveMimeType_StillMatches()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("song.flac", "Audio/FLAC");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresVideoMimeTypes()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("movie.mp4", "video/mp4");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task HandleAsync_IgnoresImageMimeTypes()
    {
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = CreateEvent("photo.png", "image/png");
        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void SupportedMimeTypes_ContainsExpectedTypes()
    {
        var types = FileUploadedMusicHandler.SupportedMimeTypes;
        Assert.IsTrue(types.Contains("audio/mpeg"));
        Assert.IsTrue(types.Contains("audio/flac"));
        Assert.IsTrue(types.Contains("audio/ogg"));
        Assert.IsTrue(types.Contains("audio/wav"));
        Assert.AreEqual(15, types.Count);
    }

    [TestMethod]
    public async Task HandleAsync_PassesCorrectParametersToCallback()
    {
        var fileNodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callbackMock = new Mock<IMusicIndexingCallback>();
        var handler = new FileUploadedMusicHandler(
            Mock.Of<ILogger<FileUploadedMusicHandler>>(),
            callbackMock.Object);

        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = fileNodeId,
            UploadedByUserId = userId,
            FileName = "track01.flac",
            MimeType = "audio/flac",
            Size = 35_000_000
        };

        await handler.HandleAsync(evt, CancellationToken.None);

        callbackMock.Verify(c => c.IndexAudioAsync(
            fileNodeId, "track01.flac", "audio/flac", 35_000_000, userId,
            It.IsAny<CancellationToken>()), Times.Once);
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
