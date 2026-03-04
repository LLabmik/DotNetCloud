using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        var service = new DownloadService(db, storageMock.Object, NullLoggerFactory.Instance.CreateLogger<DownloadService>());

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

        var service = new DownloadService(db, Mock.Of<IFileStorageEngine>(), NullLoggerFactory.Instance.CreateLogger<DownloadService>());

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.DownloadCurrentAsync(folder.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_NonExistentNode_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = new DownloadService(db, Mock.Of<IFileStorageEngine>(), NullLoggerFactory.Instance.CreateLogger<DownloadService>());

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

        var service = new DownloadService(db, storageMock.Object, NullLoggerFactory.Instance.CreateLogger<DownloadService>());

        await using var stream = await service.DownloadVersionAsync(version.Id, UserCaller(userId));

        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.AreEqual("v1 data", content);
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
}
