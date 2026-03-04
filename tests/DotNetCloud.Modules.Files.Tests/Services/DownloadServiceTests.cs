using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class DownloadServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static DownloadService CreateService(FilesDbContext db, IFileStorageEngine? storage = null) =>
        new(db, storage ?? Mock.Of<IFileStorageEngine>(), NullLogger<DownloadService>.Instance, new PermissionService(db));

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task DownloadCurrentAsync_ExistingFile_ReturnsStream()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkData = Encoding.UTF8.GetBytes("hello");

        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "hash1", StoragePath = "chunks/ha/sh/hash1", Size = chunkData.Length };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = chunkData.Length,
            ContentHash = "hash1",
            StoragePath = "files/test",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);

        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var service = CreateService(db, storageMock.Object);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.AreEqual("hello", content);
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_Folder_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.DownloadCurrentAsync(folder.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_NonExistentNode_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.DownloadCurrentAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task DownloadVersionAsync_SpecificVersion_ReturnsStream()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkData = Encoding.UTF8.GetBytes("v1 data");

        var node = new FileNode { Name = "versioned.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "v1hash", StoragePath = "chunks/v1/ha/v1hash", Size = chunkData.Length };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = chunkData.Length,
            ContentHash = "v1hash",
            StoragePath = "files/v1",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);
        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var service = CreateService(db, storageMock.Object);

        await using var stream = await service.DownloadVersionAsync(version.Id, UserCaller(userId));

        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.AreEqual("v1 data", content);
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_ReturnsOrderedHashes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);

        var chunk1 = new FileChunk { ChunkHash = "aaa111", StoragePath = "chunks/aa/a1/aaa111", Size = 100 };
        var chunk2 = new FileChunk { ChunkHash = "bbb222", StoragePath = "chunks/bb/b2/bbb222", Size = 200 };
        db.FileChunks.AddRange(chunk1, chunk2);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 300,
            ContentHash = "manifest",
            StoragePath = "files/test",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);

        db.FileVersionChunks.Add(new FileVersionChunk { FileVersionId = version.Id, FileChunkId = chunk1.Id, SequenceIndex = 0 });
        db.FileVersionChunks.Add(new FileVersionChunk { FileVersionId = version.Id, FileChunkId = chunk2.Id, SequenceIndex = 1 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var manifest = await service.GetChunkManifestAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(2, manifest.Count);
        Assert.AreEqual("aaa111", manifest[0]);
        Assert.AreEqual("bbb222", manifest[1]);
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_Folder_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.GetChunkManifestAsync(folder.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_NonExistentNode_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetChunkManifestAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_NoVersions_ReturnsEmpty()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "empty.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var manifest = await service.GetChunkManifestAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(0, manifest.Count);
    }

    // --- Phase 1.5: Chunk-by-hash download ---

    [TestMethod]
    public async Task DownloadChunkByHashAsync_ExistingChunk_ReturnsStream()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkData = Encoding.UTF8.GetBytes("chunk content");
        var chunk = new FileChunk { ChunkHash = "abc123", StoragePath = "chunks/ab/c1/abc123", Size = chunkData.Length, ReferenceCount = 1 };
        db.FileChunks.Add(chunk);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync("chunks/ab/c1/abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(chunkData));

        var service = CreateService(db, storageMock.Object);
        var stream = await service.DownloadChunkByHashAsync("abc123", UserCaller(userId));

        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.AreEqual("chunk content", await reader.ReadToEndAsync());
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_NonExistentHash_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.DownloadChunkByHashAsync("nonexistent", UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }

    // --- Phase 1.5: ConcatenatedStream seeking ---

    [TestMethod]
    public void ConcatenatedStream_CanSeek_WhenAllInnerStreamsAreSeekable()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),
            new MemoryStream(Encoding.UTF8.GetBytes("World"))
        };

        using var concat = new ConcatenatedStream(streams);

        Assert.IsTrue(concat.CanSeek);
    }

    [TestMethod]
    public void ConcatenatedStream_Length_SumsAllInnerStreams()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),  // 6 bytes
            new MemoryStream(Encoding.UTF8.GetBytes("World"))    // 5 bytes
        };

        using var concat = new ConcatenatedStream(streams);

        Assert.AreEqual(11, concat.Length);
    }

    [TestMethod]
    public async Task ConcatenatedStream_SeekToMiddle_ReadsFromCorrectPosition()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),  // 0-5
            new MemoryStream(Encoding.UTF8.GetBytes("World!"))   // 6-11
        };

        using var concat = new ConcatenatedStream(streams);

        // Seek to position 6 (start of second stream)
        concat.Seek(6, SeekOrigin.Begin);

        Assert.AreEqual(6, concat.Position);

        using var reader = new StreamReader(concat);
        var result = await reader.ReadToEndAsync();
        Assert.AreEqual("World!", result);
    }

    [TestMethod]
    public async Task ConcatenatedStream_SeekToBeginning_ReadsFromStart()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),
            new MemoryStream(Encoding.UTF8.GetBytes("World!"))
        };

        using var concat = new ConcatenatedStream(streams);

        // Read part first, then seek back to beginning
        var buf = new byte[6];
        await concat.ReadExactlyAsync(buf);

        concat.Seek(0, SeekOrigin.Begin);
        Assert.AreEqual(0, concat.Position);

        using var reader = new StreamReader(concat);
        var result = await reader.ReadToEndAsync();
        Assert.AreEqual("Hello World!", result);
    }

    [TestMethod]
    public async Task ConcatenatedStream_SeekIntoFirstStream_ReadsPartially()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),  // 0-5
            new MemoryStream(Encoding.UTF8.GetBytes("World!"))   // 6-11
        };

        using var concat = new ConcatenatedStream(streams);

        // Seek to position 3 (middle of first stream)
        concat.Seek(3, SeekOrigin.Begin);

        using var reader = new StreamReader(concat);
        var result = await reader.ReadToEndAsync();
        Assert.AreEqual("lo World!", result);
    }

    [TestMethod]
    public async Task ConcatenatedStream_MultipleChunks_ConcatenatesCorrectly()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),
            new MemoryStream(Encoding.UTF8.GetBytes("World")),
            new MemoryStream(Encoding.UTF8.GetBytes("!"))
        };

        await using var concat = new ConcatenatedStream(streams);
        using var reader = new StreamReader(concat);
        var result = await reader.ReadToEndAsync();

        Assert.AreEqual("Hello World!", result);
    }

    [TestMethod]
    public void ConcatenatedStream_Position_TracksReadProgress()
    {
        var streams = new List<Stream>
        {
            new MemoryStream(Encoding.UTF8.GetBytes("Hello ")),
            new MemoryStream(Encoding.UTF8.GetBytes("World!"))
        };

        using var concat = new ConcatenatedStream(streams);
        Assert.AreEqual(0, concat.Position);

        var buf = new byte[4];
        concat.ReadExactly(buf, 0, 4);
        Assert.AreEqual(4, concat.Position);
    }
}
