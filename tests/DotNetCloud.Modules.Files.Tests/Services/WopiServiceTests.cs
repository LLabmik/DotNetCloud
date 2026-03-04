using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class WopiServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CallerContext UserCaller(Guid userId) =>
        new(userId, Array.Empty<string>(), CallerType.User);

    private static WopiService CreateService(
        FilesDbContext db,
        IFileStorageEngine? storage = null,
        IDownloadService? download = null,
        IEventBus? eventBus = null)
    {
        return new WopiService(
            db,
            download ?? CreateMockDownloadService(),
            storage ?? Mock.Of<IFileStorageEngine>(),
            new PermissionService(db),
            eventBus ?? Mock.Of<IEventBus>(),
            NullLogger<WopiService>.Instance);
    }

    private static IDownloadService CreateMockDownloadService()
    {
        var mock = new Mock<IDownloadService>();
        mock.Setup(d => d.DownloadCurrentAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("file content")));
        return mock.Object;
    }

    // --- CheckFileInfoAsync ---

    [TestMethod]
    public async Task CheckFileInfoAsync_ExistingFile_ReturnsInfo()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode
        {
            Name = "report.docx",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 1024,
            ContentHash = "abc123",
            CurrentVersion = 3
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckFileInfoAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("report.docx", result.BaseFileName);
        Assert.AreEqual(userId.ToString(), result.OwnerId);
        Assert.AreEqual(1024, result.Size);
        Assert.IsTrue(result.UserCanWrite);
        Assert.AreEqual("abc123", result.SHA256);
    }

    [TestMethod]
    public async Task CheckFileInfoAsync_NonexistentFile_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.CheckFileInfoAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckFileInfoAsync_DeletedFile_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "deleted.docx", NodeType = FileNodeType.File, OwnerId = userId, IsDeleted = true };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckFileInfoAsync(node.Id, UserCaller(userId));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckFileInfoAsync_NoPermission_ReturnsNull()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var node = new FileNode { Name = "private.docx", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckFileInfoAsync(node.Id, UserCaller(otherId));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckFileInfoAsync_ReadOnlyShare_UserCanWriteIsFalse()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var node = new FileNode { Name = "shared.docx", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = viewerId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckFileInfoAsync(node.Id, UserCaller(viewerId));

        Assert.IsNotNull(result);
        Assert.IsFalse(result.UserCanWrite);
    }

    // --- GetFileAsync ---

    [TestMethod]
    public async Task GetFileAsync_ExistingFile_ReturnsStreamAndMime()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "test.txt", NodeType = FileNodeType.File, OwnerId = userId, MimeType = "text/plain" };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetFileAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("text/plain", result.Value.MimeType);
        Assert.AreEqual("test.txt", result.Value.FileName);
        Assert.IsNotNull(result.Value.Content);
    }

    [TestMethod]
    public async Task GetFileAsync_NonexistentFile_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetFileAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFileAsync_NullMimeType_DefaultsToOctetStream()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "unknown.bin", NodeType = FileNodeType.File, OwnerId = userId, MimeType = null };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetFileAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("application/octet-stream", result.Value.MimeType);
    }

    // --- PutFileAsync ---

    [TestMethod]
    public async Task PutFileAsync_ValidFile_CreatesNewVersion()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode
        {
            Name = "editable.docx",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            CurrentVersion = 1,
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.WriteChunkAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var eventBusMock = new Mock<IEventBus>();

        var service = CreateService(db, storageMock.Object, eventBus: eventBusMock.Object);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Updated document content"));
        await service.PutFileAsync(node.Id, content, UserCaller(userId));

        // Reload node
        var updatedNode = await db.FileNodes.FindAsync(node.Id);
        Assert.IsNotNull(updatedNode);
        Assert.AreEqual(2, updatedNode.CurrentVersion);
        Assert.IsTrue(updatedNode.Size > 0);
        Assert.IsNotNull(updatedNode.ContentHash);

        // Verify version was created
        var versions = await db.FileVersions.Where(v => v.FileNodeId == node.Id).ToListAsync();
        Assert.AreEqual(1, versions.Count);
        Assert.AreEqual(2, versions[0].VersionNumber);

        // Verify event was published
        eventBusMock.Verify(
            e => e.PublishAsync(It.IsAny<Events.FileUploadedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PutFileAsync_NonexistentFile_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        await Assert.ThrowsExactlyAsync<Core.Errors.NotFoundException>(
            () => service.PutFileAsync(Guid.NewGuid(), content, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task PutFileAsync_Folder_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.PutFileAsync(folder.Id, content, UserCaller(userId)));
    }

    [TestMethod]
    public async Task PutFileAsync_NoWritePermission_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var node = new FileNode { Name = "readonly.docx", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = viewerId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        await Assert.ThrowsExactlyAsync<Core.Errors.ForbiddenException>(
            () => service.PutFileAsync(node.Id, content, UserCaller(viewerId)));
    }

    [TestMethod]
    public async Task PutFileAsync_ChunkDeduplication_ReusesExistingChunk()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "dedup.docx", NodeType = FileNodeType.File, OwnerId = userId, CurrentVersion = 1 };
        db.FileNodes.Add(node);

        // Pre-create a chunk with known hash
        var data = Encoding.UTF8.GetBytes("chunk content for dedup test");
        var hash = ContentHasher.ComputeHash(data);
        var existingChunk = new FileChunk
        {
            ChunkHash = hash,
            StoragePath = ContentHasher.GetChunkStoragePath(hash),
            Size = data.Length,
            ReferenceCount = 1
        };
        db.FileChunks.Add(existingChunk);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        var service = CreateService(db, storageMock.Object, eventBus: Mock.Of<IEventBus>());

        using var content = new MemoryStream(data);
        await service.PutFileAsync(node.Id, content, UserCaller(userId));

        // The chunk's reference count should be incremented, not a new chunk created
        var chunk = await db.FileChunks.FirstAsync(c => c.ChunkHash == hash);
        Assert.AreEqual(2, chunk.ReferenceCount);

        // Storage should NOT be called since the chunk already exists
        storageMock.Verify(
            s => s.WriteChunkAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
