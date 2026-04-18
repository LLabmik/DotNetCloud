using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        new(db, storage ?? Mock.Of<IFileStorageEngine>(), NullLogger<DownloadService>.Instance, new PermissionService(db),
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()));

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
    public async Task DownloadCurrentAsync_ZeroByteFile_ReturnsEmptyStream()
    {
        // 0-byte files have a single chunk with Size=0 and the SHA-256 of empty content as the hash.
        // The blob should never be read from storage; the service must return an empty stream.
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        const string emptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        var node = new FileNode { Name = "err.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 0 };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = emptyHash, StoragePath = $"chunks/e3/b0/{emptyHash}", Size = 0 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 0,
            ContentHash = emptyHash,
            StoragePath = "files/empty",
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

        // Storage engine is NOT configured — blob must not be requested
        var storageMock = new Mock<IFileStorageEngine>(MockBehavior.Strict);
        var service = CreateService(db, storageMock.Object);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(stream);
        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_MissingChunkBlob_ThrowsNotFoundException()
    {
        // Non-empty file whose chunk blob was lost from storage must return NotFoundException (→ 404),
        // not InvalidOperationException (→ 400).
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var node = new FileNode { Name = "create_admin.cs", NodeType = FileNodeType.File, OwnerId = userId, Size = 512 };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "fd250474abc", StoragePath = "chunks/fd/25/fd250474abc", Size = 512 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 512,
            ContentHash = "fd250474abc",
            StoragePath = "files/fd25/fd250474abc",
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

        // Storage returns null — blob is missing from disk
        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var service = CreateService(db, storageMock.Object);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.DownloadCurrentAsync(node.Id, UserCaller(userId)));
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

        var node = new FileNode
        {
            Name = "chunk-owner.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId
        };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "abc123", StoragePath = "chunks/ab/c1/abc123", Size = chunkData.Length, ReferenceCount = 1 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = chunkData.Length,
            ContentHash = "abc123",
            StoragePath = "files/chunk-owner",
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

    [TestMethod]
    public async Task DownloadZipAsync_MissingChunkBlob_ThrowsNotFoundException()
    {
        // When a chunk blob is missing from storage during ZIP assembly, the service
        // must throw NotFoundException — not silently skip the chunk and produce a truncated file.
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var node = new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = userId, Size = 1024 };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "deadbeef1234", StoragePath = "chunks/de/ad/deadbeef1234", Size = 1024 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 1024,
            ContentHash = "deadbeef1234",
            StoragePath = "files/dead/deadbeef1234",
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

        // Storage returns null — blob is missing from disk
        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.OpenReadStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var service = CreateService(db, storageMock.Object);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.DownloadZipAsync([node.Id], UserCaller(userId)));
    }
}
