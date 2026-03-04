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
        var chunkHash = "abc123";
        var chunkData = new byte[512];

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
}
