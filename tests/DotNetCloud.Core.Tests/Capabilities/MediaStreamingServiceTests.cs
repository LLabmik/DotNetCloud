namespace DotNetCloud.Core.Tests.Capabilities;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for <see cref="IMediaStreamingService"/> and <see cref="MediaStreamResult"/>.
/// </summary>
[TestClass]
public class MediaStreamingServiceTests
{
    [TestMethod]
    public void IMediaStreamingService_ImplementsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(IMediaStreamingService)));
    }

    [TestMethod]
    public async Task OpenStreamAsync_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IMediaStreamingService>();
        var fileNodeId = Guid.NewGuid();
        var stream = new MemoryStream(new byte[100]);

        mock.Setup(x => x.OpenStreamAsync(fileNodeId, 0, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaStreamResult
            {
                Stream = stream,
                ContentType = "audio/mpeg",
                TotalLength = 100,
                RangeStart = 0,
                RangeEnd = 99
            });

        // Act
        var result = await mock.Object.OpenStreamAsync(fileNodeId, 0, 99);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("audio/mpeg", result.ContentType);
        Assert.AreEqual(100, result.TotalLength);
        result.Dispose();
    }

    [TestMethod]
    public void DetectContentType_CanBeMocked()
    {
        // Arrange
        var mock = new Mock<IMediaStreamingService>();
        mock.Setup(x => x.DetectContentType("song.mp3")).Returns("audio/mpeg");
        mock.Setup(x => x.DetectContentType("unknown.xyz")).Returns("application/octet-stream");

        // Act & Assert
        Assert.AreEqual("audio/mpeg", mock.Object.DetectContentType("song.mp3"));
        Assert.AreEqual("application/octet-stream", mock.Object.DetectContentType("unknown.xyz"));
    }

    [TestMethod]
    public void MediaStreamResult_IsPartial_TrueWhenNotFullRange()
    {
        // Arrange
        using var result = new MediaStreamResult
        {
            Stream = new MemoryStream(),
            ContentType = "video/mp4",
            TotalLength = 1000,
            RangeStart = 500,
            RangeEnd = 999
        };

        // Act & Assert
        Assert.IsTrue(result.IsPartial);
    }

    [TestMethod]
    public void MediaStreamResult_IsPartial_FalseWhenFullRange()
    {
        // Arrange
        using var result = new MediaStreamResult
        {
            Stream = new MemoryStream(),
            ContentType = "video/mp4",
            TotalLength = 1000,
            RangeStart = 0,
            RangeEnd = 999
        };

        // Act & Assert
        Assert.IsFalse(result.IsPartial);
    }

    [TestMethod]
    public void MediaStreamResult_ContentLength_CalculatesCorrectly()
    {
        // Arrange
        using var result = new MediaStreamResult
        {
            Stream = new MemoryStream(),
            ContentType = "audio/flac",
            TotalLength = 10_000_000,
            RangeStart = 1000,
            RangeEnd = 5000
        };

        // Act & Assert
        Assert.AreEqual(4001, result.ContentLength);
    }

    [TestMethod]
    public void MediaStreamResult_ContentLength_SingleByte()
    {
        // Arrange
        using var result = new MediaStreamResult
        {
            Stream = new MemoryStream(),
            ContentType = "audio/flac",
            TotalLength = 100,
            RangeStart = 50,
            RangeEnd = 50
        };

        // Act & Assert
        Assert.AreEqual(1, result.ContentLength);
    }

    [TestMethod]
    public void MediaStreamResult_ContentLength_FullFile()
    {
        // Arrange
        using var result = new MediaStreamResult
        {
            Stream = new MemoryStream(),
            ContentType = "image/jpeg",
            TotalLength = 5000,
            RangeStart = 0,
            RangeEnd = 4999
        };

        // Act & Assert
        Assert.AreEqual(5000, result.ContentLength);
    }

    [TestMethod]
    public void MediaStreamResult_Dispose_DisposesStream()
    {
        // Arrange
        var stream = new MemoryStream();
        var result = new MediaStreamResult
        {
            Stream = stream,
            ContentType = "video/mp4",
            TotalLength = 100,
            RangeStart = 0,
            RangeEnd = 99
        };

        // Act
        result.Dispose();

        // Assert — accessing disposed stream should throw
        Assert.ThrowsExactly<ObjectDisposedException>(() => stream.ReadByte());
    }
}
