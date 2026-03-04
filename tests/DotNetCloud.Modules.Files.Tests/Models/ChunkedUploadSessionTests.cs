using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="ChunkedUploadSession"/> entity covering defaults and properties.
/// </summary>
[TestClass]
public class ChunkedUploadSessionTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.AreNotEqual(Guid.Empty, session.Id);
    }

    [TestMethod]
    public void WhenCreatedThenStatusIsInProgress()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.AreEqual(UploadSessionStatus.InProgress, session.Status);
    }

    [TestMethod]
    public void WhenCreatedThenReceivedChunksIsZero()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.AreEqual(0, session.ReceivedChunks);
    }

    [TestMethod]
    public void WhenCreatedThenTotalSizeIsZero()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.AreEqual(0, session.TotalSize);
    }

    [TestMethod]
    public void WhenCreatedThenExpiresAtIsInTheFuture()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.IsTrue(session.ExpiresAt > DateTime.UtcNow);
    }

    [TestMethod]
    public void WhenCreatedThenExpiresAtIsApproximately24HoursFromNow()
    {
        var before = DateTime.UtcNow.AddHours(23);
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };
        var after = DateTime.UtcNow.AddHours(25);

        Assert.IsTrue(session.ExpiresAt >= before && session.ExpiresAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenTimestampsAreRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(session.CreatedAt >= before && session.CreatedAt <= after);
        Assert.IsTrue(session.UpdatedAt >= before && session.UpdatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenTargetFieldsAreNull()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        Assert.IsNull(session.TargetFileNodeId);
        Assert.IsNull(session.TargetParentId);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var fileNodeId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var session = new ChunkedUploadSession
        {
            TargetFileNodeId = fileNodeId,
            TargetParentId = parentId,
            FileName = "large-file.zip",
            TotalSize = 10485760,
            MimeType = "application/zip",
            TotalChunks = 3,
            ReceivedChunks = 1,
            ChunkManifest = "[\"hash1\",\"hash2\",\"hash3\"]",
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };

        Assert.AreEqual(fileNodeId, session.TargetFileNodeId);
        Assert.AreEqual(parentId, session.TargetParentId);
        Assert.AreEqual("large-file.zip", session.FileName);
        Assert.AreEqual(10485760, session.TotalSize);
        Assert.AreEqual("application/zip", session.MimeType);
        Assert.AreEqual(3, session.TotalChunks);
        Assert.AreEqual(1, session.ReceivedChunks);
        Assert.AreEqual(userId, session.UserId);
    }

    [TestMethod]
    public void WhenStatusChangedToCompletedThenStoresNewStatus()
    {
        var session = new ChunkedUploadSession { FileName = "test.bin", ChunkManifest = "[]" };

        session.Status = UploadSessionStatus.Completed;

        Assert.AreEqual(UploadSessionStatus.Completed, session.Status);
    }

}
