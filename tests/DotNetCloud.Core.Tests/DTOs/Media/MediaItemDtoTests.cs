namespace DotNetCloud.Core.Tests.DTOs.Media;

using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaItemDto"/> record.
/// </summary>
[TestClass]
public class MediaItemDtoTests
{
    [TestMethod]
    public void MediaItemDto_RequiredProperties_AreInitialized()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        // Act
        var dto = new MediaItemDto
        {
            FileNodeId = fileNodeId,
            MediaType = MediaType.Photo,
            FileName = "sunset.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_880,
            OwnerId = ownerId,
            CreatedAtUtc = now
        };

        // Assert
        Assert.AreEqual(fileNodeId, dto.FileNodeId);
        Assert.AreEqual(MediaType.Photo, dto.MediaType);
        Assert.AreEqual("sunset.jpg", dto.FileName);
        Assert.AreEqual("image/jpeg", dto.MimeType);
        Assert.AreEqual(5_242_880, dto.SizeBytes);
        Assert.AreEqual(ownerId, dto.OwnerId);
        Assert.AreEqual(now, dto.CreatedAtUtc);
        Assert.IsNull(dto.ModifiedAtUtc);
        Assert.IsNull(dto.Metadata);
    }

    [TestMethod]
    public void MediaItemDto_WithMetadata_SetsNestedMetadata()
    {
        // Arrange
        var metadata = new MediaMetadataDto
        {
            MediaType = MediaType.Audio,
            Title = "Test Song",
            Artist = "Test Artist",
            Duration = TimeSpan.FromSeconds(180)
        };

        // Act
        var dto = new MediaItemDto
        {
            FileNodeId = Guid.NewGuid(),
            MediaType = MediaType.Audio,
            FileName = "song.mp3",
            MimeType = "audio/mpeg",
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Metadata = metadata
        };

        // Assert
        Assert.IsNotNull(dto.Metadata);
        Assert.AreEqual("Test Song", dto.Metadata.Title);
        Assert.AreEqual("Test Artist", dto.Metadata.Artist);
        Assert.AreEqual(TimeSpan.FromSeconds(180), dto.Metadata.Duration);
    }

    [TestMethod]
    public void MediaItemDto_WithModifiedDate_SetsOptionalField()
    {
        // Arrange
        var created = DateTime.UtcNow.AddDays(-7);
        var modified = DateTime.UtcNow;

        // Act
        var dto = new MediaItemDto
        {
            FileNodeId = Guid.NewGuid(),
            MediaType = MediaType.Video,
            FileName = "clip.mp4",
            MimeType = "video/mp4",
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = created,
            ModifiedAtUtc = modified
        };

        // Assert
        Assert.AreEqual(created, dto.CreatedAtUtc);
        Assert.AreEqual(modified, dto.ModifiedAtUtc);
    }

    [TestMethod]
    public void MediaItemDto_Equality_SameValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var dto1 = new MediaItemDto
        {
            FileNodeId = id,
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            OwnerId = ownerId,
            CreatedAtUtc = now
        };

        var dto2 = new MediaItemDto
        {
            FileNodeId = id,
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            OwnerId = ownerId,
            CreatedAtUtc = now
        };

        // Act & Assert
        Assert.AreEqual(dto1, dto2);
    }

    [TestMethod]
    public void MediaItemDto_NotEqual_DifferentFileNodeId()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var dto1 = new MediaItemDto
        {
            FileNodeId = Guid.NewGuid(),
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            OwnerId = ownerId,
            CreatedAtUtc = now
        };

        var dto2 = new MediaItemDto
        {
            FileNodeId = Guid.NewGuid(),
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            OwnerId = ownerId,
            CreatedAtUtc = now
        };

        // Act & Assert
        Assert.AreNotEqual(dto1, dto2);
    }

    [TestMethod]
    public void MediaItemDto_ZeroSizeBytes_IsValid()
    {
        // Arrange & Act
        var dto = new MediaItemDto
        {
            FileNodeId = Guid.NewGuid(),
            MediaType = MediaType.Photo,
            FileName = "empty.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 0,
            OwnerId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, dto.SizeBytes);
    }
}
