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

    // ---- CDC (Content-Defined Chunking) Tests ----

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_SmallData_ReturnsSingleChunk()
    {
        // Data must be strictly below minSize so Phase 1 consumes everything and
        // Phase 2 (boundary detection) is never entered — guaranteeing one chunk.
        var data = new byte[256]; // 256 B < minSize 512 B
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream, minSize: 512, avgSize: 2048, maxSize: 4096);

        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual(0L, chunks[0].Offset);
        Assert.AreEqual(data.Length, chunks[0].Size);
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_EmptyStream_ReturnsNoChunks()
    {
        using var stream = new MemoryStream([]);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream);

        Assert.AreEqual(0, chunks.Count);
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_ChunkHashesMatchData()
    {
        // 128KB of content with tiny minSize so we get multiple chunks
        var data = new byte[128 * 1024];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream, minSize: 1024, avgSize: 4096, maxSize: 16384);

        foreach (var chunk in chunks)
        {
            var chunkData = data.AsSpan((int)chunk.Offset, chunk.Size).ToArray();
            var expected = ContentHasher.ComputeHash(chunkData);
            Assert.AreEqual(expected, chunk.Hash, $"Hash mismatch for chunk at offset {chunk.Offset}");
        }
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_ConcatenatedChunksEqualOriginal()
    {
        var data = new byte[256 * 1024]; // 256 KB
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream, minSize: 1024, avgSize: 8192, maxSize: 32768);

        var totalSize = chunks.Sum(c => c.Size);
        Assert.AreEqual(data.Length, totalSize, "Total chunk sizes must equal original data length.");

        // Verify offsets are contiguous
        long expectedOffset = 0;
        foreach (var chunk in chunks)
        {
            Assert.AreEqual(expectedOffset, chunk.Offset, $"Chunk offset should be {expectedOffset} at chunk index.");
            expectedOffset += chunk.Size;
        }
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_ModifiedByte_ChangesContainingChunkHash()
    {
        // Identical streams except one byte — the chunk containing that byte must have a different hash.
        const int size = 64 * 1024;
        const int pivotByte = 1000;

        var data1 = new byte[size];
        var data2 = new byte[size];
        Random.Shared.NextBytes(data1);
        data1.CopyTo(data2, 0);
        // Flip one byte to force a different hash in the containing chunk
        data2[pivotByte] ^= 0xFF;

        using var s1 = new MemoryStream(data1);
        using var s2 = new MemoryStream(data2);

        var chunks1 = await ContentHasher.ChunkAndHashCdcAsync(s1, minSize: 512, avgSize: 2048, maxSize: 8192);
        var chunks2 = await ContentHasher.ChunkAndHashCdcAsync(s2, minSize: 512, avgSize: 2048, maxSize: 8192);

        // The chunk that spans pivotByte must differ
        var containing1 = chunks1.First(c => c.Offset <= pivotByte && c.Offset + c.Size > pivotByte);
        var containing2 = chunks2.First(c => c.Offset <= pivotByte && c.Offset + c.Size > pivotByte);
        Assert.AreNotEqual(containing1.Hash, containing2.Hash,
            "The chunk containing the modified byte must have a different hash.");
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_HashReturnedIs64LowercaseHexChars()
    {
        var data = new byte[8192];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream, minSize: 512, avgSize: 1024, maxSize: 4096);

        foreach (var chunk in chunks)
        {
            Assert.AreEqual(64, chunk.Hash.Length);
            Assert.AreEqual(chunk.Hash, chunk.Hash.ToLowerInvariant());
        }
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_MaxSizeRespected()
    {
        const int maxSize = 4096;
        var data = new byte[maxSize * 10]; // Force many hard-cutoff chunks
        new Random(42).NextBytes(data); // Mostly uniform → no content boundaries
        using var stream = new MemoryStream(data);

        var chunks = await ContentHasher.ChunkAndHashCdcAsync(stream, minSize: 16, avgSize: 1 << 30, maxSize: maxSize);

        foreach (var chunk in chunks)
            Assert.IsTrue(chunk.Size <= maxSize, $"Chunk size {chunk.Size} exceeds maxSize {maxSize}");
    }

    [TestMethod]
    public async Task ChunkAndHashCdcAsync_DeterministicForSameInput()
    {
        var data = new byte[32 * 1024];
        Random.Shared.NextBytes(data);

        using var s1 = new MemoryStream(data);
        using var s2 = new MemoryStream(data);

        var chunks1 = await ContentHasher.ChunkAndHashCdcAsync(s1, minSize: 1024, avgSize: 4096, maxSize: 16384);
        var chunks2 = await ContentHasher.ChunkAndHashCdcAsync(s2, minSize: 1024, avgSize: 4096, maxSize: 16384);

        Assert.AreEqual(chunks1.Count, chunks2.Count);
        for (var i = 0; i < chunks1.Count; i++)
        {
            Assert.AreEqual(chunks1[i].Hash, chunks2[i].Hash);
            Assert.AreEqual(chunks1[i].Offset, chunks2[i].Offset);
            Assert.AreEqual(chunks1[i].Size, chunks2[i].Size);
        }
    }
}
