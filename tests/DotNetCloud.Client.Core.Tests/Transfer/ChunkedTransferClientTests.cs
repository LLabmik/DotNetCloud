using System.Security.Cryptography;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.Core.Tests.Transfer;

[TestClass]
public class ChunkedTransferClientTests
{
    // ── UploadAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_SmallFile_UploadsAsOneChunk()
    {
        var nodeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);

        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        apiMock.Setup(a => a.CompleteUploadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteUploadResponse
            {
                Node = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" },
            });

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);
        using var data = new MemoryStream(new byte[1024]);

        var result = await client.UploadAsync(null, "test.txt", data, null);

        Assert.AreEqual(nodeId, result);
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
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
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid?, long, string?, IReadOnlyList<string>, CancellationToken>(
                (_, _, _, _, hashes, _) => chunkHash = hashes[0])
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

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);
        using var data = new MemoryStream(new byte[512]);

        await client.UploadAsync(null, "dedup.txt", data, null);

        // Chunk should NOT be uploaded (deduplication)
        apiMock.Verify(a => a.UploadChunkAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
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
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // First upload call throws a network error; second succeeds
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, int, string, Stream, CancellationToken>((_, _, _, _, _) =>
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

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);
        using var data = new MemoryStream(new byte[512]);

        var result = await client.UploadAsync(null, "file.txt", data, null);

        Assert.AreEqual(nodeId, result);
        // Should have been called twice (one failure + one success)
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task UploadAsync_NetworkErrorExhaustsRetries_Throws()
    {
        var sessionId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // Always throw network error
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);
        using var data = new MemoryStream(new byte[512]);

        HttpRequestException? caught = null;
        try { await client.UploadAsync(null, "file.txt", data, null); }
        catch (HttpRequestException ex) { caught = ex; }
        Assert.IsNotNull(caught, "Expected HttpRequestException but none was thrown.");

        // Should have been called ChunkUploadMaxRetries (3) times
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task UploadAsync_ClientError_DoesNotRetry()
    {
        var sessionId = Guid.NewGuid();

        var apiMock = new Mock<IDotNetCloudApiClient>();
        apiMock.SetupProperty(a => a.AccessToken);
        apiMock.Setup(a => a.InitiateUploadAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<long>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionResponse { SessionId = sessionId });

        // 4xx client error — should NOT be retried
        apiMock.Setup(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden));

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);
        using var data = new MemoryStream(new byte[512]);

        HttpRequestException? caught = null;
        try { await client.UploadAsync(null, "file.txt", data, null); }
        catch (HttpRequestException ex) { caught = ex; }
        Assert.IsNotNull(caught, "Expected HttpRequestException but none was thrown.");

        // Should have been called exactly once (no retry on 4xx)
        apiMock.Verify(a => a.UploadChunkAsync(sessionId, 0, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
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

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);

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

        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()), Times.Once);
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
        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new MemoryStream(callCount == 1 ? corruptData : chunkData);
            });

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);

        using var result = await client.DownloadAsync(nodeId);

        Assert.IsNotNull(result);
        Assert.AreEqual(chunkData.Length, result.Length);
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()), Times.Exactly(2));
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

        apiMock.Setup(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(corruptData));

        var client = new ChunkedTransferClient(apiMock.Object, NullLogger<ChunkedTransferClient>.Instance);

        ChunkIntegrityException? caught = null;
        try { await client.DownloadAsync(nodeId); }
        catch (ChunkIntegrityException ex) { caught = ex; }

        Assert.IsNotNull(caught, "Expected ChunkIntegrityException but none was thrown.");

        // Should have retried 3 times
        apiMock.Verify(a => a.DownloadChunkByHashAsync(chunkHash, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
