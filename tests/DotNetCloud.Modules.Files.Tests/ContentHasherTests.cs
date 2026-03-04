using DotNetCloud.Modules.Files.Services;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="ContentHasher"/> covering hashing, chunking, and path generation.
/// </summary>
[TestClass]
public class ContentHasherTests
{
    [TestMethod]
    public void WhenComputeHashCalledThenReturnsLowercaseHex64Chars()
    {
        var data = "hello world"u8.ToArray();

        var hash = ContentHasher.ComputeHash(data);

        Assert.IsNotNull(hash);
        Assert.AreEqual(64, hash.Length);
        Assert.AreEqual(hash, hash.ToLowerInvariant());
    }

    [TestMethod]
    public void WhenSameDataHashedTwiceThenReturnsIdenticalHash()
    {
        var data = "deterministic"u8.ToArray();

        var hash1 = ContentHasher.ComputeHash(data);
        var hash2 = ContentHasher.ComputeHash(data);

        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void WhenDifferentDataHashedThenReturnsDifferentHashes()
    {
        var data1 = "alpha"u8.ToArray();
        var data2 = "beta"u8.ToArray();

        var hash1 = ContentHasher.ComputeHash(data1);
        var hash2 = ContentHasher.ComputeHash(data2);

        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public async Task WhenComputeHashAsyncCalledThenReturnsCorrectHash()
    {
        var data = "hello world"u8.ToArray();
        using var stream = new MemoryStream(data);

        var streamHash = await ContentHasher.ComputeHashAsync(stream);
        var directHash = ContentHasher.ComputeHash(data);

        Assert.AreEqual(directHash, streamHash);
    }

    [TestMethod]
    public async Task WhenChunkAndHashAsyncCalledWithSmallDataThenReturnsSingleChunk()
    {
        var data = "small data"u8.ToArray();
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashAsync(stream);

        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual(data.Length, chunks[0].Data.Length);
    }

    [TestMethod]
    public async Task WhenChunkAndHashAsyncCalledWithMultipleChunksThenReturnsCorrectCount()
    {
        // Create data larger than one chunk (use 16 byte chunks for testing)
        var data = new byte[48];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashAsync(stream, chunkSize: 16);

        Assert.AreEqual(3, chunks.Count);
    }

    [TestMethod]
    public async Task WhenChunkAndHashAsyncCalledThenChunkHashesAreValid()
    {
        var data = new byte[32];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashAsync(stream, chunkSize: 16);

        foreach (var (hash, chunkData) in chunks)
        {
            Assert.AreEqual(64, hash.Length);
            Assert.AreEqual(ContentHasher.ComputeHash(chunkData), hash);
        }
    }

    [TestMethod]
    public async Task WhenChunkAndHashAsyncCalledThenConcatenatedChunksEqualOriginal()
    {
        var data = new byte[100];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashAsync(stream, chunkSize: 32);

        var reassembled = chunks.SelectMany(c => c.Data).ToArray();

        CollectionAssert.AreEqual(data, reassembled);
    }

    [TestMethod]
    public void WhenComputeManifestHashCalledThenReturnsConsistentHash()
    {
        var hashes = new[] { "aaa", "bbb", "ccc" };

        var hash1 = ContentHasher.ComputeManifestHash(hashes);
        var hash2 = ContentHasher.ComputeManifestHash(hashes);

        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void WhenComputeManifestHashCalledWithDifferentOrderThenReturnsDifferentHash()
    {
        var hashes1 = new[] { "aaa", "bbb" };
        var hashes2 = new[] { "bbb", "aaa" };

        var hash1 = ContentHasher.ComputeManifestHash(hashes1);
        var hash2 = ContentHasher.ComputeManifestHash(hashes2);

        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void WhenGetChunkStoragePathCalledThenReturnsContentAddressablePath()
    {
        var hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        var path = ContentHasher.GetChunkStoragePath(hash);

        Assert.AreEqual("chunks/ab/cd/abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", path);
    }

    [TestMethod]
    public void WhenGetFileStoragePathCalledThenReturnsContentAddressablePath()
    {
        var hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        var path = ContentHasher.GetFileStoragePath(hash);

        Assert.AreEqual("files/ab/cd/abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", path);
    }

    [TestMethod]
    public void WhenGetChunkStoragePathCalledWithNullThenThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentHasher.GetChunkStoragePath(null!));
    }

    [TestMethod]
    public void WhenGetChunkStoragePathCalledWithEmptyThenThrows()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ContentHasher.GetChunkStoragePath(""));
    }

    [TestMethod]
    public void WhenDefaultChunkSizeThenIs4MB()
    {
#pragma warning disable MSTEST0032 // Canary test guards the compile-time constant value
        Assert.AreEqual(4 * 1024 * 1024, ContentHasher.DefaultChunkSize);
#pragma warning restore MSTEST0032
    }
}
