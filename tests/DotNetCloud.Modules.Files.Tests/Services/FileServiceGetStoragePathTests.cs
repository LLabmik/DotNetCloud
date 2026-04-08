using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class FileServiceGetStoragePathTests
{
    private static FilesDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FileService CreateService(FilesDbContext db) =>
        new(db, Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<FileService>(),
            new PermissionService(db), new DeviceContext(),
            Mock.Of<IQuotaService>(),
            Microsoft.Extensions.Options.Options.Create(new FileSystemOptions()),
            Mock.Of<ISyncChangeNotifier>());

    private static FileNode CreateFileNode(Guid ownerId, string storagePath = "ab/cd/abcdef1234567890")
    {
        var id = Guid.NewGuid();
        return new FileNode
        {
            Id = id,
            Name = "test-file.jpg",
            NodeType = FileNodeType.File,
            OwnerId = ownerId,
            StoragePath = storagePath,
            ContentHash = "abcdef1234567890",
            MimeType = "image/jpeg",
            Size = 1024,
            MaterializedPath = $"/{id}",
            Depth = 0
        };
    }

    // ─── File Node Found ─────────────────────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_FileNodeExists_ReturnsStoragePath()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId, "ab/cd/abcdef1234567890");
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoragePathAsync(node.Id);

        Assert.AreEqual("ab/cd/abcdef1234567890", result);
    }

    // ─── File Node Not Found ─────────────────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_NodeDoesNotExist_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetStoragePathAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    // ─── Folder Node Returns Null ────────────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_FolderNode_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var folder = new FileNode
        {
            Id = folderId,
            Name = "Photos",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            StoragePath = null,
            MaterializedPath = $"/{folderId}",
            Depth = 0
        };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoragePathAsync(folderId);

        Assert.IsNull(result);
    }

    // ─── Symbolic Link Returns Null ──────────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_SymbolicLinkNode_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new FileNode
        {
            Id = linkId,
            Name = "shortcut.lnk",
            NodeType = FileNodeType.SymbolicLink,
            OwnerId = userId,
            StoragePath = "ab/cd/target-hash",
            MaterializedPath = $"/{linkId}",
            Depth = 0
        };
        db.FileNodes.Add(link);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoragePathAsync(linkId);

        Assert.IsNull(result);
    }

    // ─── Soft-Deleted File Still Accessible (IgnoreQueryFilters) ─────

    [TestMethod]
    public async Task GetStoragePathAsync_SoftDeletedFile_StillReturnsPath()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId, "ab/cd/deleted-file-hash");
        node.IsDeleted = true;
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoragePathAsync(node.Id);

        Assert.AreEqual("ab/cd/deleted-file-hash", result);
    }

    // ─── Null StoragePath On File ────────────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_FileWithNullStoragePath_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var node = new FileNode
        {
            Id = nodeId,
            Name = "incomplete.jpg",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            StoragePath = null,
            MaterializedPath = $"/{nodeId}",
            Depth = 0
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetStoragePathAsync(nodeId);

        Assert.IsNull(result);
    }

    // ─── Cancellation Token Propagation ──────────────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_CancellationRequested_ThrowsOrReturns()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateFileNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // In-memory provider may not throw on cancellation, but the call should complete
        // Either throws OperationCanceledException or returns normally
        try
        {
            await service.GetStoragePathAsync(node.Id, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — provider respects cancellation
        }
    }

    // ─── Different Files Return Different Paths ──────────────────────

    [TestMethod]
    public async Task GetStoragePathAsync_MultipleFiles_ReturnsCorrectPathForEach()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = CreateFileNode(userId, "aa/bb/hash1");
        var node2 = CreateFileNode(userId, "cc/dd/hash2");
        db.FileNodes.AddRange(node1, node2);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.AreEqual("aa/bb/hash1", await service.GetStoragePathAsync(node1.Id));
        Assert.AreEqual("cc/dd/hash2", await service.GetStoragePathAsync(node2.Id));
    }
}
