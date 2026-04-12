using System.Security.Cryptography;
using System.Text.Json;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.Transfer;

[TestClass]
public class ChunkedTransferClientTests
{
    private string _testCacheDir = null!;

    [TestInitialize]
    public void TestSetup() =>
        _testCacheDir = Path.Combine(Path.GetTempPath(), $"dnc-test-cache-{Guid.NewGuid():N}");

    [TestCleanup]
    public void TestTeardown()
    {
        if (Directory.Exists(_testCacheDir))
            Directory.Delete(_testCacheDir, recursive: true);
    }

    private ChunkedTransferClient CreateClient(IDotNetCloudApiClient api, ILocalStateDb? stateDb = null) =>
        new(api, stateDb, NullLogger<ChunkedTransferClient>.Instance)
        {
            ChunkCacheDirectory = _testCacheDir,
        };

    [TestMethod]
    public async Task UploadAsync_SmallFile_UploadsAsOneChunk()
    {
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);

        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[1024]);

        var result = await client.UploadAsync(null, "test.txt", data, null);

        Assert.AreEqual(nodeId, result);
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        apiMock.Verify(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadAsync_ServerHasChunk_SkipsUpload()
    {
        var sessionId = Guid.NewGuid();
        var chunkHash = string.Empty;

        // Capture the chunk hash from InitiateUpload
        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, IReadOnlyList<int>?, int?, string?, string?, CancellationToken>(
                (_, _, _, _, hashes, _, _, _, _, _) => chunkHash = hashes[0])
            .ReturnsAsync(() => new UploadSessionResponse
            {
                SessionId = sessionId,
                PresentChunks = [chunkHash], // server already has the chunk
            });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = Guid.NewGuid(), Name = "dedup.txt", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);

        await client.UploadAsync(null, "dedup.txt", data, null);

        // Chunk should NOT be uploaded (deduplication)
        apiMock.Verify(a => a.UploadChunkAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [TestMethod]
    public async Task UploadAsync_NetworkErrorOnFirstAttempt_RetriesAndSucceeds()
    {
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var callCount = 0;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // First upload call throws a network error; second succeeds
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns<Guid, int, string, Stream, CancellationToken, string?>((_, _, _, _, _, _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("Connection reset by peer");
                return Task.CompletedTask;
            });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "file.txt", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);

        var result = await client.UploadAsync(null, "file.txt", data, null);

        Assert.AreEqual(nodeId, result);
        // Should have been called twice (one failure + one success)
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task UploadAsync_NetworkErrorExhaustsRetries_Throws()
    {
        var sessionId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // Always throw network error
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);

        HttpRequestException? caught = null;
        try
        { await client.UploadAsync(null, "file.txt", data, null); }
        catch (HttpRequestException ex) { caught = ex; }
        Assert.IsNotNull(caught, "Expected HttpRequestException but none was thrown.");

        // Should have been called ChunkUploadMaxRetries (3) times
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task UploadAsync_ClientError_DoesNotRetry()
    {
        var sessionId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // 4xx client error — should NOT be retried
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ThrowsAsync(new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden));

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);

        HttpRequestException? caught = null;
        try
        { await client.UploadAsync(null, "file.txt", data, null); }
        catch (HttpRequestException ex) { caught = ex; }
        Assert.IsNotNull(caught, "Expected HttpRequestException but none was thrown.");

        // Should have been called exactly once (no retry on 4xx)
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
    }

    // ── DownloadAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DownloadAsync_EmptyManifest_FallsBackToDirectDownload()
    {
        var nodeId = Guid.NewGuid();
        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse()); // empty manifest

        apiMock.Setup(a => a.DownloadAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        apiMock.Verify(a => a.DownloadAsync(nodeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DownloadAsync_WithManifest_DownloadsChunks()
    {
        var nodeId = Guid.NewGuid();
        var chunkData = new byte[512];
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = chunkData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = chunkData.Length }],
            });

        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DownloadAsync_ChunkHashMismatch_RetriesAndSucceeds()
    {
        var nodeId = Guid.NewGuid();
        var chunkData = new byte[512];
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));
        var corruptData = new byte[512];
        corruptData[0] = 0xFF; // one byte differs → different hash

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = chunkData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = chunkData.Length }],
            });

        // First call returns corrupt data, second returns correct data
        var callCount = 0;
        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new MemoryStream(callCount == 1 ? corruptData : chunkData);
            });

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task DownloadAsync_ChunkHashAlwaysMismatch_ThrowsChunkIntegrityException()
    {
        var nodeId = Guid.NewGuid();
        var chunkHash = "0000000000000000000000000000000000000000000000000000000000000000";
        var corruptData = new byte[512]; // SHA-256 of this does NOT equal chunkHash

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = corruptData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = corruptData.Length }],
            });

        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(corruptData));

        var client = CreateClient(apiMock.Object);

        ChunkIntegrityException? caught = null;
        try
        { await client.DownloadAsync(nodeId); }
        catch (ChunkIntegrityException ex) { caught = ex; }

        Assert.IsNotNull(caught, "Expected ChunkIntegrityException but none was thrown.");

        // Should have retried 3 times
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    // ── CDC chunking ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_CdcChunking_SendsChunkSizesWithHashes()
    {
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        IReadOnlyList<int>? capturedSizes = null;
        IReadOnlyList<string>? capturedHashes = null;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, IReadOnlyList<int>?, int?, string?, string?, CancellationToken>(
                (_, _, _, _, hashes, sizes, _, _, _, _) => { capturedHashes = hashes; capturedSizes = sizes; })
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        apiMock.Setup(a => a.UploadChunkAsync(sessionId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "file.bin", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[1024]);

        await client.UploadAsync(null, "file.bin", data, null);

        Assert.IsNotNull(capturedSizes, "ChunkSizes should be passed to InitiateUploadAsync.");
        Assert.IsNotNull(capturedHashes);
        Assert.AreEqual(capturedHashes!.Count, capturedSizes!.Count, "ChunkSizes count must equal chunk hash count.");
        Assert.IsTrue(capturedSizes.All(s => s > 0), "All chunk sizes must be positive.");
        Assert.AreEqual(capturedHashes.Count, capturedSizes.Sum(s => s) == 1024 ? capturedHashes.Count : capturedHashes.Count,
            "Sum of chunk sizes must equal file size.");
        Assert.AreEqual(1024, capturedSizes.Sum(), "Sum of chunk sizes must equal total file size.");
    }

    [TestMethod]
    public async Task UploadAsync_CdcChunking_DeterministicAcrossMultipleCalls()
    {
        // CDC is deterministic: chunking the same data twice yields identical hashes.
        var fileData = new byte[2 * 1024 * 1024]; // 2 MB of pseudo-random data
        new Random(42).NextBytes(fileData);

        List<string> capturedHashes1 = [];
        List<string> capturedHashes2 = [];
        var captureTarget = capturedHashes1;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, IReadOnlyList<int>?, int?, string?, string?, CancellationToken>(
                (_, _, _, _, hashes, _, _, _, _, _) => captureTarget.AddRange(hashes))
            .ReturnsAsync(new UploadSessionResponse { SessionId = Guid.NewGuid() });

        apiMock.Setup(a => a.UploadChunkAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        apiMock.Setup(a => a.CompleteUploadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = Guid.NewGuid(), Name = "f.bin", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);

        await client.UploadAsync(null, "f.bin", new MemoryStream(fileData), null);
        captureTarget = capturedHashes2;
        await client.UploadAsync(null, "f.bin", new MemoryStream(fileData), null);

        Assert.IsTrue(capturedHashes1.Count > 0, "Expected at least one chunk.");
        CollectionAssert.AreEqual(capturedHashes1, capturedHashes2,
            "CDC chunking must produce identical hashes for identical input.");
    }

    // ── Streaming pipeline (Task 2.2) ───────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_StreamingPipeline_BoundedMemoryUsage()
    {
        // Verifies that the channel-based pipeline correctly uploads all missing chunks.
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var uploadCallCount = 0;
        IReadOnlyList<string>? capturedHashes = null;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, IReadOnlyList<int>?, int?, string?, string?, CancellationToken>(
                (_, _, _, _, hashes, _, _, _, _, _) => capturedHashes = hashes)
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId }); // no present chunks → all must upload

        apiMock.Setup(a => a.UploadChunkAsync(sessionId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns<Guid, int, string, Stream, CancellationToken, string?>((_, _, _, _, _, _) =>
            {
                Interlocked.Increment(ref uploadCallCount);
                return Task.CompletedTask;
            });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "big.bin", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        // Use 1 MB of data — small enough for tests but exercises the pipeline
        using var data = new MemoryStream(new byte[1024 * 1024]);

        var result = await client.UploadAsync(null, "big.bin", data, null);

        Assert.AreEqual(nodeId, result);
        Assert.IsNotNull(capturedHashes);
        // All chunks should have been uploaded (none pre-existing)
        Assert.AreEqual(capturedHashes!.Count, uploadCallCount,
            "Upload call count must equal the number of missing chunks.");
    }

    [TestMethod]
    public async Task DownloadAsync_StreamingToTempFiles_AssemblesCorrectly()
    {
        // Verifies that temp-file-based download reassembles all chunks in correct order.
        var nodeId = Guid.NewGuid();

        var chunk0 = new byte[512];
        var chunk1 = new byte[512];
        new Random(1).NextBytes(chunk0);
        new Random(2).NextBytes(chunk1);
        var hash0 = Convert.ToHexStringLower(SHA256.HashData(chunk0));
        var hash1 = Convert.ToHexStringLower(SHA256.HashData(chunk1));

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = 1024,
                Chunks =
                [
                    new ChunkManifestEntry { Index = 0, Hash = hash0, Size = 512 },
                    new ChunkManifestEntry { Index = 1, Hash = hash1, Size = 512 },
                ],
            });

        apiMock.Setup(a => a.DownloadChunkByHashAsync(hash0, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(chunk0));
        apiMock.Setup(a => a.DownloadChunkByHashAsync(hash1, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(chunk1));

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(1024L, result.Length, "Output stream length must equal sum of chunk sizes.");

        // Verify chunk ordering: first 512 bytes match chunk0, next 512 match chunk1
        result.Seek(0, SeekOrigin.Begin);
        var assembled = new byte[1024];
        var read = await result.ReadAsync(assembled.AsMemory(0, 1024));
        Assert.AreEqual(1024, read);
        CollectionAssert.AreEqual(chunk0, assembled[..512], "First chunk content mismatch.");
        CollectionAssert.AreEqual(chunk1, assembled[512..], "Second chunk content mismatch.");

        apiMock.Verify(a => a.DownloadChunkByHashAsync(hash0, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(hash1, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Session Persistence (Task 3.2) ─────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_PersistsAndDeletesSessionRecord_OnSuccess()
    {
        // Verifies that a session record is saved after InitiateUpload and deleted after Complete.
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });
        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" },
            });

        var dbMock = new Mock<ILocalStateDb>();
        dbMock.Setup(d => d.GetActiveUploadSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = CreateClient(apiMock.Object, dbMock.Object);
        using var data = new MemoryStream(new byte[1024]);

        await client.UploadAsync(null, "test.txt", data, null, default, "/state/db");

        dbMock.Verify(d => d.SaveActiveUploadSessionAsync(
            "/state/db",
            It.Is<ActiveUploadSessionRecord>(r => r.SessionId == sessionId && r.LocalPath == "test.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
        dbMock.Verify(d => d.DeleteActiveUploadSessionAsync(
            "/state/db", sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadAsync_ResumesSession_SkipsAlreadyUploadedChunks()
    {
        // Verifies that when an existing session has chunk hashes recorded,
        // those chunks are not re-uploaded (treated as already present).
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var fileData = new byte[1024];
        new Random(42).NextBytes(fileData);
        // Single chunk (< CdcMinSize) — hash = SHA256 of entire file.
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(fileData));

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, fileData);
            var knownModifiedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(tempFile, knownModifiedAt);

            var existingSession = new ActiveUploadSessionRecord
            {
                SessionId = sessionId,
                LocalPath = tempFile,
                FileSize = fileData.Length,
                FileModifiedAt = knownModifiedAt,
                TotalChunks = 1,
                UploadedChunkHashesJson = JsonSerializer.Serialize(new[] { chunkHash }),
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            };

            var apiMock = new Mock<IDotNetCloudApiClient>();
            apiMock.SetupProperty(a => a.AccessToken);
            apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CompleteUploadResponse
                {
                    Node = new FileNodeResponse { Id = nodeId, Name = Path.GetFileName(tempFile), NodeType = "File" },
                });

            var dbMock = new Mock<ILocalStateDb>();
            dbMock.Setup(d => d.GetActiveUploadSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingSession]);

            var client = CreateClient(apiMock.Object, dbMock.Object);
            using var stream = File.OpenRead(tempFile);

            var result = await client.UploadAsync(null, tempFile, stream, null, default, "/state/db");

            // Resume path: InitiateUploadAsync must NOT be called
            apiMock.Verify(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            // Single chunk already uploaded — UploadChunkAsync must NOT be called
            apiMock.Verify(a => a.UploadChunkAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
            // Complete called with the resumed session ID
            apiMock.Verify(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(nodeId, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task UploadAsync_StaleSession_DeletesRecordAndStartsFresh()
    {
        // Verifies that a session > 48 h old is discarded and a fresh upload is initiated.
        var oldSessionId = Guid.NewGuid();
        var newSessionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var existingSession = new ActiveUploadSessionRecord
        {
            SessionId = oldSessionId,
            LocalPath = "test.txt",
            FileSize = 1024,
            FileModifiedAt = DateTime.UtcNow.AddHours(-50),
            TotalChunks = 1,
            UploadedChunkHashesJson = "[]",
            CreatedAt = DateTime.UtcNow.AddHours(-50), // > 48 h old
        };

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = newSessionId });
        apiMock.Setup(a => a.CompleteUploadAsync(newSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" },
            });

        var dbMock = new Mock<ILocalStateDb>();
        dbMock.Setup(d => d.GetActiveUploadSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingSession]);

        var client = CreateClient(apiMock.Object, dbMock.Object);
        using var data = new MemoryStream(new byte[1024]);

        await client.UploadAsync(null, "test.txt", data, null, default, "/state/db");

        // Old session deleted
        dbMock.Verify(d => d.DeleteActiveUploadSessionAsync(
            "/state/db", oldSessionId, It.IsAny<CancellationToken>()), Times.Once);
        // Fresh InitiateUpload called
        apiMock.Verify(a => a.InitiateUploadAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
            It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.CompleteUploadAsync(newSessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadAsync_FileChanged_DeletesSessionAndStartsFresh()
    {
        // Verifies that if the file size differs from the session record, the old session
        // is discarded and a new upload is initiated.
        var oldSessionId = Guid.NewGuid();
        var newSessionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var existingSession = new ActiveUploadSessionRecord
        {
            SessionId = oldSessionId,
            LocalPath = "test.txt",
            FileSize = 2048, // different from the 1024-byte stream below
            FileModifiedAt = DateTime.UtcNow.AddMinutes(-5),
            TotalChunks = 1,
            UploadedChunkHashesJson = "[]",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        };

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = newSessionId });
        apiMock.Setup(a => a.CompleteUploadAsync(newSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" },
            });

        var dbMock = new Mock<ILocalStateDb>();
        dbMock.Setup(d => d.GetActiveUploadSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingSession]);

        var client = CreateClient(apiMock.Object, dbMock.Object);
        using var data = new MemoryStream(new byte[1024]); // 1024 != 2048 in session

        await client.UploadAsync(null, "test.txt", data, null, default, "/state/db");

        // Old session deleted because file changed
        dbMock.Verify(d => d.DeleteActiveUploadSessionAsync(
            "/state/db", oldSessionId, It.IsAny<CancellationToken>()), Times.Once);
        // Fresh upload initiated
        apiMock.Verify(a => a.InitiateUploadAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
            It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.CompleteUploadAsync(newSessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Chunk cache ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DownloadAsync_CacheMiss_DownloadsAndCachesChunk()
    {
        var nodeId = Guid.NewGuid();
        var chunkData = new byte[512];
        new Random(42).NextBytes(chunkData);
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = chunkData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = chunkData.Length }],
            });
        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        // API was called exactly once (cache was empty)
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        // Cache file was created
        var cacheFile = Path.Combine(_testCacheDir, chunkHash);
        Assert.IsTrue(File.Exists(cacheFile), "Chunk should have been written to cache.");
        CollectionAssert.AreEqual(chunkData, await File.ReadAllBytesAsync(cacheFile));
    }

    [TestMethod]
    public async Task DownloadAsync_CacheHit_SkipsApiCall()
    {
        var nodeId = Guid.NewGuid();
        var chunkData = new byte[512];
        new Random(99).NextBytes(chunkData);
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));

        // Pre-populate cache
        Directory.CreateDirectory(_testCacheDir);
        await File.WriteAllBytesAsync(Path.Combine(_testCacheDir, chunkHash), chunkData);

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = chunkData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = chunkData.Length }],
            });

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        // API should NOT have been called — data came from cache
        apiMock.Verify(a => a.DownloadChunkByHashAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
    }

    // ── POSIX metadata (Issue #42) ──────────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_PassesPosixModeToInitiateUpload()
    {
        int? capturedPosixMode = -1;
        string? capturedOwnerHint = "unset";
        var sessionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock
            .Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, IReadOnlyList<int>?, int?, string?, string?, CancellationToken>(
                (_, _, _, _, _, _, pm, oh, _, _) => { capturedPosixMode = pm; capturedOwnerHint = oh; })
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });
        apiMock
            .Setup(a => a.UploadChunkAsync(sessionId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        apiMock
            .Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse { Node = new FileNodeResponse { Id = nodeId, Name = "f.txt", NodeType = "File" } });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);
        await client.UploadAsync(null, "f.txt", data, null, default, null, posixMode: 420, posixOwnerHint: "bob:devs");

        Assert.AreEqual(420, capturedPosixMode);
        Assert.AreEqual("bob:devs", capturedOwnerHint);
    }

    // ── TaskCanceledException retry (Issue #59) ─────────────────────────────

    [TestMethod]
    public async Task UploadAsync_TimeoutOnFirstAttempt_RetriesAndSucceeds()
    {
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var callCount = 0;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // First upload call throws TaskCanceledException (timeout); second succeeds
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns<Guid, int, string, Stream, CancellationToken, string?>((_, _, _, _, _, _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
                return Task.CompletedTask;
            });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "file.txt", NodeType = "File" },
            });

        var client = CreateClient(apiMock.Object);
        using var data = new MemoryStream(new byte[512]);

        var result = await client.UploadAsync(null, "file.txt", data, null);

        Assert.AreEqual(nodeId, result);
        // Should have been called twice (one timeout + one success)
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task DownloadAsync_TimeoutOnFirstAttempt_RetriesAndSucceeds()
    {
        var nodeId = Guid.NewGuid();
        var chunkData = new byte[512];
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));
        var callCount = 0;

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.GetChunkManifestAsync(nodeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChunkManifestResponse
            {
                TotalSize = chunkData.Length,
                Chunks = [new ChunkManifestEntry { Index = 0, Hash = chunkHash, Size = chunkData.Length }],
            });

        // First call throws timeout, second returns data
        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<string, bool, CancellationToken>((_, _, _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
                return Task.FromResult<Stream>(new MemoryStream(chunkData));
            });

        var client = CreateClient(apiMock.Object);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Session resume window (Issue #61) ───────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_SessionAt20Hours_StillResumes()
    {
        // Session is 20h old — used to be discarded at 18h, now should resume at 48h window.
        var sessionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var fileData = new byte[1024];
        new Random(42).NextBytes(fileData);
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(fileData));

        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, fileData);
            var knownModifiedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(tempFile, knownModifiedAt);

            var existingSession = new ActiveUploadSessionRecord
            {
                SessionId = sessionId,
                LocalPath = tempFile,
                FileSize = fileData.Length,
                FileModifiedAt = knownModifiedAt,
                TotalChunks = 1,
                UploadedChunkHashesJson = JsonSerializer.Serialize(new[] { chunkHash }),
                CreatedAt = DateTime.UtcNow.AddHours(-20), // 20h old — within new 48h window
            };

            var apiMock = new Mock<IDotNetCloudApiClient>();
            apiMock.SetupProperty(a => a.AccessToken);
            apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CompleteUploadResponse
                {
                    Node = new FileNodeResponse { Id = nodeId, Name = Path.GetFileName(tempFile), NodeType = "File" },
                });

            var dbMock = new Mock<ILocalStateDb>();
            dbMock.Setup(d => d.GetActiveUploadSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([existingSession]);

            var client = CreateClient(apiMock.Object, dbMock.Object);
            using var stream = File.OpenRead(tempFile);

            var result = await client.UploadAsync(null, tempFile, stream, null, default, "/state/db");

            // Should resume, NOT start fresh (InitiateUploadAsync not called)
            apiMock.Verify(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<int>?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            apiMock.Verify(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(nodeId, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}







