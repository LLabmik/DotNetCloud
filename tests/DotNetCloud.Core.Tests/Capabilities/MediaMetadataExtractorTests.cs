namespace DotNetCloud.Core.Tests.Capabilities;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for <see cref="IMediaMetadataExtractor"/> interface.
/// </summary>
[TestClass]
public class MediaMetadataExtractorTests
{
    [TestMethod]
    public void IMediaMetadataExtractor_ImplementsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(IMediaMetadataExtractor)));
    }

    [TestMethod]
    public void CanExtract_WithMock_ReturnsTrueForSupportedType()
    {
        // Arrange
        var mock = new Mock<IMediaMetadataExtractor>();
        mock.Setup(x => x.SupportedMediaType).Returns(MediaType.Photo);
        mock.Setup(x => x.CanExtract("image/jpeg")).Returns(true);
        mock.Setup(x => x.CanExtract("audio/mpeg")).Returns(false);

        // Act & Assert
        Assert.IsTrue(mock.Object.CanExtract("image/jpeg"));
        Assert.IsFalse(mock.Object.CanExtract("audio/mpeg"));
    }

    [TestMethod]
    public async Task ExtractAsync_WithMock_ReturnsMetadata()
    {
        // Arrange
        var mock = new Mock<IMediaMetadataExtractor>();
        var expected = new MediaMetadataDto
        {
            MediaType = MediaType.Audio,
            Title = "Test Song",
            Artist = "Test Artist",
            Duration = TimeSpan.FromSeconds(180)
        };

        mock.Setup(x => x.ExtractAsync("/path/to/song.mp3", "audio/mpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await mock.Object.ExtractAsync("/path/to/song.mp3", "audio/mpeg");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Test Song", result.Title);
        Assert.AreEqual("Test Artist", result.Artist);
        Assert.AreEqual(TimeSpan.FromSeconds(180), result.Duration);
    }

    [TestMethod]
    public async Task ExtractAsync_WithMock_ReturnsNullForUnsupported()
    {
        // Arrange
        var mock = new Mock<IMediaMetadataExtractor>();
        mock.Setup(x => x.ExtractAsync(It.IsAny<string>(), "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaMetadataDto?)null);

        // Act
        var result = await mock.Object.ExtractAsync("/path/to/doc.pdf", "application/pdf");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SupportedMediaType_ReturnsConfiguredType()
    {
        // Arrange
        var mock = new Mock<IMediaMetadataExtractor>();
        mock.Setup(x => x.SupportedMediaType).Returns(MediaType.Video);

        // Act & Assert
        Assert.AreEqual(MediaType.Video, mock.Object.SupportedMediaType);
    }
}
