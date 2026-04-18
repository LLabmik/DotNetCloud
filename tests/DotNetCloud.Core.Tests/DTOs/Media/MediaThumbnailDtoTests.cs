namespace DotNetCloud.Core.Tests.DTOs.Media;

using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaThumbnailSize"/> enum and <see cref="MediaThumbnailDto"/> record.
/// </summary>
[TestClass]
public class MediaThumbnailDtoTests
{
    [TestMethod]
    public void MediaThumbnailSize_HasExpectedPixelValues()
    {
        // Assert
        Assert.AreEqual(128, (int)MediaThumbnailSize.Small);
        Assert.AreEqual(300, (int)MediaThumbnailSize.Grid);
        Assert.AreEqual(512, (int)MediaThumbnailSize.Medium);
        Assert.AreEqual(1200, (int)MediaThumbnailSize.Large);
    }

    [TestMethod]
    public void MediaThumbnailSize_HasExactlyFourValues()
    {
        // Act
        var values = Enum.GetValues<MediaThumbnailSize>();

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void MediaThumbnailDto_RequiredProperties_AreSet()
    {
        // Arrange & Act
        var id = Guid.NewGuid();
        var dto = new MediaThumbnailDto
        {
            FileNodeId = id,
            Size = MediaThumbnailSize.Grid,
            ContentType = "image/jpeg",
            Width = 300,
            Height = 200
        };

        // Assert
        Assert.AreEqual(id, dto.FileNodeId);
        Assert.AreEqual(MediaThumbnailSize.Grid, dto.Size);
        Assert.AreEqual("image/jpeg", dto.ContentType);
        Assert.AreEqual(300, dto.Width);
        Assert.AreEqual(200, dto.Height);
        Assert.IsNull(dto.Url);
    }

    [TestMethod]
    public void MediaThumbnailDto_WithUrl_SetsOptionalField()
    {
        // Arrange & Act
        var dto = new MediaThumbnailDto
        {
            FileNodeId = Guid.NewGuid(),
            Size = MediaThumbnailSize.Small,
            ContentType = "image/jpeg",
            Url = "/api/media/thumbnails/abc123/small"
        };

        // Assert
        Assert.AreEqual("/api/media/thumbnails/abc123/small", dto.Url);
    }

    [TestMethod]
    public void MediaThumbnailDto_Equality_SameValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        var dto1 = new MediaThumbnailDto
        {
            FileNodeId = id,
            Size = MediaThumbnailSize.Medium,
            ContentType = "image/jpeg",
            Width = 512,
            Height = 384
        };

        var dto2 = new MediaThumbnailDto
        {
            FileNodeId = id,
            Size = MediaThumbnailSize.Medium,
            ContentType = "image/jpeg",
            Width = 512,
            Height = 384
        };

        // Act & Assert
        Assert.AreEqual(dto1, dto2);
    }
}
