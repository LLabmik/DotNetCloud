using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests.Models;

/// <summary>
/// Tests for <see cref="FileChunk"/> entity covering defaults and properties.
/// </summary>
[TestClass]
public class FileChunkTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var chunk = new FileChunk { ChunkHash = "abc", StoragePath = "/path" };

        Assert.AreNotEqual(Guid.Empty, chunk.Id);
    }

    [TestMethod]
    public void WhenCreatedThenReferenceCountIsOne()
    {
        var chunk = new FileChunk { ChunkHash = "abc", StoragePath = "/path" };

        Assert.AreEqual(1, chunk.ReferenceCount);
    }

    [TestMethod]
    public void WhenCreatedThenSizeIsZero()
    {
        var chunk = new FileChunk { ChunkHash = "abc", StoragePath = "/path" };

        Assert.AreEqual(0, chunk.Size);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var chunk = new FileChunk { ChunkHash = "abc", StoragePath = "/path" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(chunk.CreatedAt >= before && chunk.CreatedAt <= after);
    }

    [TestMethod]
    public void WhenCreatedThenLastReferencedAtIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var chunk = new FileChunk { ChunkHash = "abc", StoragePath = "/path" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(chunk.LastReferencedAt >= before && chunk.LastReferencedAt <= after);
    }

    [TestMethod]
    public void WhenPropertiesSetThenStoresValues()
    {
        var chunk = new FileChunk
        {
            ChunkHash = "abc123def456",
            Size = 4194304,
            StoragePath = "chunks/ab/c1/abc123def456",
            ReferenceCount = 5
        };

        Assert.AreEqual("abc123def456", chunk.ChunkHash);
        Assert.AreEqual(4194304, chunk.Size);
        Assert.AreEqual("chunks/ab/c1/abc123def456", chunk.StoragePath);
        Assert.AreEqual(5, chunk.ReferenceCount);
    }
}
