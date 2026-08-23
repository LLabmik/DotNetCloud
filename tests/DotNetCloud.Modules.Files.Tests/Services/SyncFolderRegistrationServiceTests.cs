using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class SyncFolderRegistrationServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.CreateVersion7().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static SyncFolderRegistrationService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<SyncFolderRegistrationService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode Folder(string name, Guid ownerId, string materializedPath) =>
        new() { Name = name, NodeType = FileNodeType.Folder, OwnerId = ownerId, MaterializedPath = materializedPath };

    private static FileNode File(string name, Guid ownerId, string materializedPath) =>
        new() { Name = name, NodeType = FileNodeType.File, OwnerId = ownerId, MaterializedPath = materializedPath };

    [TestMethod]
    public async Task RegisterAsync_ValidFolder_CreatesRegistration()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var folder = Folder("Documents", userId, "/documents");
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var dto = await service.RegisterAsync(folder.Id, UserCaller(userId));

        Assert.IsNotNull(dto);
        Assert.AreEqual(folder.Id, dto.RemoteFolderNodeId);
        Assert.AreEqual("/Documents", dto.RemoteFolderPath);

        var row = await db.SyncFolderRegistrations.SingleAsync(r => r.RemoteFolderNodeId == folder.Id);
        Assert.IsTrue(row.IsActive);
        Assert.AreEqual(userId, row.UserId);
    }

    [TestMethod]
    public async Task RegisterAsync_NodeNotFound_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RegisterAsync(Guid.CreateVersion7(), UserCaller(Guid.CreateVersion7())));
    }

    [TestMethod]
    public async Task RegisterAsync_NodeNotOwnedByCaller_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.CreateVersion7();
        var folder = Folder("Private", ownerId, "/private");
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RegisterAsync(folder.Id, UserCaller(Guid.CreateVersion7())));
    }

    [TestMethod]
    public async Task RegisterAsync_NodeIsFile_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var file = File("report.pdf", userId, "/report.pdf");
        db.FileNodes.Add(file);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await Assert.ThrowsAsync<ValidationException>(
            () => service.RegisterAsync(file.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task RegisterAsync_FolderInsideExistingRegistration_Rejected()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var parent = Folder("Documents", userId, "/documents");
        var child = Folder("BigFiles", userId, "/documents/bigfiles");
        db.FileNodes.AddRange(parent, child);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RegisterAsync(parent.Id, UserCaller(userId));

        await Assert.ThrowsAsync<ValidationException>(
            () => service.RegisterAsync(child.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task RegisterAsync_ExistingRegistrationInsideFolder_Rejected()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var parent = Folder("Documents", userId, "/documents");
        var child = Folder("BigFiles", userId, "/documents/bigfiles");
        db.FileNodes.AddRange(parent, child);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RegisterAsync(child.Id, UserCaller(userId));

        await Assert.ThrowsAsync<ValidationException>(
            () => service.RegisterAsync(parent.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task RegisterAsync_SameFolderTwice_ReturnsExisting()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var folder = Folder("Documents", userId, "/documents");
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var first = await service.RegisterAsync(folder.Id, UserCaller(userId));
        var second = await service.RegisterAsync(folder.Id, UserCaller(userId));

        Assert.AreEqual(first.Id, second.Id);
        Assert.AreEqual(1, db.SyncFolderRegistrations.Count(r => r.RemoteFolderNodeId == folder.Id && r.IsActive));
    }

    [TestMethod]
    public async Task RegisterAsync_DisjointSiblings_BothAllowed()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var a = Folder("Docs", userId, "/docs");
        var b = Folder("Photos", userId, "/photos");
        db.FileNodes.AddRange(a, b);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RegisterAsync(a.Id, UserCaller(userId));
        var dtoB = await service.RegisterAsync(b.Id, UserCaller(userId));

        Assert.IsNotNull(dtoB);
        Assert.AreEqual(b.Id, dtoB.RemoteFolderNodeId);
    }

    [TestMethod]
    public async Task UnregisterAsync_ExistingFolder_MarksInactive()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var folder = Folder("Documents", userId, "/documents");
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RegisterAsync(folder.Id, UserCaller(userId));

        await service.UnregisterAsync(folder.Id, UserCaller(userId));

        var row = await db.SyncFolderRegistrations.SingleAsync(r => r.RemoteFolderNodeId == folder.Id);
        Assert.IsFalse(row.IsActive);
    }

    [TestMethod]
    public async Task UnregisterAsync_NotRegistered_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UnregisterAsync(Guid.CreateVersion7(), UserCaller(Guid.CreateVersion7())));
    }

    [TestMethod]
    public async Task ListAsync_ReturnsOnlyCallersActiveRegistrations()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var folder = Folder("Documents", userId, "/documents");
        var otherFolder = Folder("Others", otherUserId, "/others");
        db.FileNodes.AddRange(folder, otherFolder);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RegisterAsync(folder.Id, UserCaller(userId));
        await service.RegisterAsync(otherFolder.Id, UserCaller(otherUserId));

        var list = await service.ListAsync(UserCaller(userId));

        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(folder.Id, list[0].RemoteFolderNodeId);
    }
}
