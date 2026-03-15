using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for bulk file operations (move, copy, delete, permanent delete).
/// Validates per-item error handling, partial success, and quota enforcement
/// matching the pattern used by <c>BulkController</c>.
/// </summary>
[TestClass]
public class BulkOperationTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FileService CreateFileService(FilesDbContext db, IQuotaService? quotaService = null) =>
        new(db, Mock.Of<IEventBus>(), NullLogger<FileService>.Instance,
            new PermissionService(db), new DeviceContext(), quotaService ?? Mock.Of<IQuotaService>(),
            Microsoft.Extensions.Options.Options.Create(new FileSystemOptions()),
            Mock.Of<ISyncChangeNotifier>());

    private static TrashService CreateTrashService(FilesDbContext db) =>
        new(db, Mock.Of<IFileStorageEngine>(), Mock.Of<IEventBus>(),
            Mock.Of<ISyncChangeNotifier>(), NullLogger<TrashService>.Instance);

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    /// <summary>
    /// Simulates the per-item-catch bulk pattern used by BulkController.
    /// </summary>
    private static async Task<List<BulkItemResultDto>> ExecuteBulkAsync(
        IReadOnlyList<Guid> nodeIds,
        Func<Guid, Task> operation)
    {
        var results = new List<BulkItemResultDto>();
        foreach (var nodeId in nodeIds)
        {
            try
            {
                await operation(nodeId);
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = true });
            }
            catch (Exception ex)
            {
                results.Add(new BulkItemResultDto { NodeId = nodeId, Success = false, Error = ex.Message });
            }
        }
        return results;
    }

    #region Bulk Move

    [TestMethod]
    public async Task BulkMove_AllItemsExist_AllSucceed()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file1 = new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(target, file1, file2);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file1.Id, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.MoveAsync(id, new MoveNodeDto { TargetParentId = target.Id }, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.Success));

        var movedFile1 = await db.FileNodes.FindAsync(file1.Id);
        var movedFile2 = await db.FileNodes.FindAsync(file2.Id);
        Assert.AreEqual(target.Id, movedFile1!.ParentId);
        Assert.AreEqual(target.Id, movedFile2!.ParentId);
    }

    [TestMethod]
    public async Task BulkMove_SomeItemsNonExistent_PartialSuccess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file1 = new FileNode { Name = "exists.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file1.MaterializedPath = $"/{file1.Id}";

        db.FileNodes.AddRange(target, file1);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nonExistentId = Guid.NewGuid();
        var nodeIds = new List<Guid> { file1.Id, nonExistentId };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.MoveAsync(id, new MoveNodeDto { TargetParentId = target.Id }, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.IsFalse(results[1].Success);
        Assert.IsNotNull(results[1].Error);
    }

    [TestMethod]
    public async Task BulkMove_FailureDoesNotPreventSubsequentItems()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file1 = new FileNode { Name = "first.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode { Name = "last.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(target, file1, file2);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nonExistentId = Guid.NewGuid();

        // Order: valid, invalid, valid — the third item should still succeed
        var nodeIds = new List<Guid> { file1.Id, nonExistentId, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.MoveAsync(id, new MoveNodeDto { TargetParentId = target.Id }, caller));

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.IsFalse(results[1].Success);
        Assert.IsTrue(results[2].Success);
    }

    [TestMethod]
    public async Task BulkMove_WrongOwner_FailsWithForbidden()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = attackerId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file = new FileNode { Name = "secret.txt", NodeType = FileNodeType.File, OwnerId = ownerId };
        file.MaterializedPath = $"/{file.Id}";

        db.FileNodes.AddRange(target, file);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var nodeIds = new List<Guid> { file.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.MoveAsync(id, new MoveNodeDto { TargetParentId = target.Id }, UserCaller(attackerId)));

        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].Success);
    }

    #endregion

    #region Bulk Copy

    [TestMethod]
    public async Task BulkCopy_AllItemsExist_AllSucceed()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file1 = new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 50 };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 75 };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(target, file1, file2);
        await db.SaveChangesAsync();

        var quotaMock = new Mock<IQuotaService>();
        quotaMock.Setup(q => q.HasSufficientQuotaAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        quotaMock.Setup(q => q.AdjustUsedBytesAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var service = CreateFileService(db, quotaMock.Object);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file1.Id, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.CopyAsync(id, target.Id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.Success));

        // Original files still exist + 2 copies
        var filesInTarget = await db.FileNodes.CountAsync(n => n.ParentId == target.Id);
        Assert.AreEqual(2, filesInTarget);
    }

    [TestMethod]
    public async Task BulkCopy_InsufficientQuota_FailsPerItem()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file1 = new FileNode { Name = "small.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 10 };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode { Name = "big.dat", NodeType = FileNodeType.File, OwnerId = userId, Size = 999999 };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(target, file1, file2);
        await db.SaveChangesAsync();

        var callCount = 0;
        var quotaMock = new Mock<IQuotaService>();
        quotaMock.Setup(q => q.HasSufficientQuotaAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() =>
                 {
                     callCount++;
                     return callCount == 1; // First copy succeeds, second fails quota
                 });
        quotaMock.Setup(q => q.AdjustUsedBytesAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var service = CreateFileService(db, quotaMock.Object);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file1.Id, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.CopyAsync(id, target.Id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.IsFalse(results[1].Success);
    }

    [TestMethod]
    public async Task BulkCopy_PartialFailure_CountsCorrectly()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";

        var file = new FileNode { Name = "valid.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 10 };
        file.MaterializedPath = $"/{file.Id}";

        db.FileNodes.AddRange(target, file);
        await db.SaveChangesAsync();

        var quotaMock = new Mock<IQuotaService>();
        quotaMock.Setup(q => q.HasSufficientQuotaAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        quotaMock.Setup(q => q.AdjustUsedBytesAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var service = CreateFileService(db, quotaMock.Object);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file.Id, Guid.NewGuid(), Guid.NewGuid() };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.CopyAsync(id, target.Id, caller));

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.AreEqual(1, successCount);
        Assert.AreEqual(2, failureCount);
        Assert.AreEqual(3, results.Count);
    }

    #endregion

    #region Bulk Delete (Soft-Delete)

    [TestMethod]
    public async Task BulkDelete_AllItemsExist_AllSoftDeleted()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var file1 = new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(file1, file2);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file1.Id, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.DeleteAsync(id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.Success));

        // Query filters hide soft-deleted
        Assert.AreEqual(0, await db.FileNodes.CountAsync());

        // But they exist with IgnoreQueryFilters
        var deletedCount = await db.FileNodes.IgnoreQueryFilters().CountAsync(n => n.IsDeleted);
        Assert.AreEqual(2, deletedCount);
    }

    [TestMethod]
    public async Task BulkDelete_SomeNonExistent_PartialSuccess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var file = new FileNode { Name = "exists.txt", NodeType = FileNodeType.File, OwnerId = userId };
        file.MaterializedPath = $"/{file.Id}";
        db.FileNodes.Add(file);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file.Id, Guid.NewGuid() };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.DeleteAsync(id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.IsFalse(results[1].Success);
    }

    [TestMethod]
    public async Task BulkDelete_FolderWithChildren_CascadesSoftDelete()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        folder.MaterializedPath = $"/{folder.Id}";

        var child = new FileNode
        {
            Name = "child.txt", NodeType = FileNodeType.File, OwnerId = userId,
            ParentId = folder.Id, Depth = 1
        };
        child.MaterializedPath = $"{folder.MaterializedPath}/{child.Id}";

        db.FileNodes.AddRange(folder, child);
        await db.SaveChangesAsync();

        var service = CreateFileService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { folder.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.DeleteAsync(id, caller));

        Assert.IsTrue(results[0].Success);

        // Both folder and child should be soft-deleted
        var deletedCount = await db.FileNodes.IgnoreQueryFilters().CountAsync(n => n.IsDeleted);
        Assert.AreEqual(2, deletedCount);
    }

    #endregion

    #region Bulk Permanent Delete

    [TestMethod]
    public async Task BulkPermanentDelete_TrashedItems_AllPermanentlyDeleted()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var file1 = new FileNode
        {
            Name = "trash1.txt", NodeType = FileNodeType.File, OwnerId = userId,
            IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedByUserId = userId
        };
        file1.MaterializedPath = $"/{file1.Id}";
        var file2 = new FileNode
        {
            Name = "trash2.txt", NodeType = FileNodeType.File, OwnerId = userId,
            IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedByUserId = userId
        };
        file2.MaterializedPath = $"/{file2.Id}";

        db.FileNodes.AddRange(file1, file2);
        await db.SaveChangesAsync();

        var trashService = CreateTrashService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file1.Id, file2.Id };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            trashService.PermanentDeleteAsync(id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results.All(r => r.Success));

        // Even with IgnoreQueryFilters, permanently deleted nodes are gone
        Assert.AreEqual(0, await db.FileNodes.IgnoreQueryFilters().CountAsync());
    }

    [TestMethod]
    public async Task BulkPermanentDelete_MixedExistAndNonExist_PartialSuccess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var file = new FileNode
        {
            Name = "trash.txt", NodeType = FileNodeType.File, OwnerId = userId,
            IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedByUserId = userId
        };
        file.MaterializedPath = $"/{file.Id}";
        db.FileNodes.Add(file);
        await db.SaveChangesAsync();

        var trashService = CreateTrashService(db);
        var caller = UserCaller(userId);
        var nodeIds = new List<Guid> { file.Id, Guid.NewGuid() };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            trashService.PermanentDeleteAsync(id, caller));

        Assert.AreEqual(2, results.Count);
        Assert.IsTrue(results[0].Success);
        Assert.IsFalse(results[1].Success);
    }

    #endregion

    #region Bulk Result DTO

    [TestMethod]
    public void BulkResultDto_CountsMatchResults()
    {
        var results = new List<BulkItemResultDto>
        {
            new() { NodeId = Guid.NewGuid(), Success = true },
            new() { NodeId = Guid.NewGuid(), Success = false, Error = "Not found" },
            new() { NodeId = Guid.NewGuid(), Success = true },
        };

        var dto = new BulkResultDto
        {
            TotalCount = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };

        Assert.AreEqual(3, dto.TotalCount);
        Assert.AreEqual(2, dto.SuccessCount);
        Assert.AreEqual(1, dto.FailureCount);
        Assert.AreEqual(3, dto.Results.Count);
    }

    [TestMethod]
    public void BulkItemResultDto_SuccessHasNoError()
    {
        var item = new BulkItemResultDto { NodeId = Guid.NewGuid(), Success = true };

        Assert.IsTrue(item.Success);
        Assert.IsNull(item.Error);
    }

    [TestMethod]
    public void BulkItemResultDto_FailureHasError()
    {
        var item = new BulkItemResultDto { NodeId = Guid.NewGuid(), Success = false, Error = "Forbidden" };

        Assert.IsFalse(item.Success);
        Assert.AreEqual("Forbidden", item.Error);
    }

    [TestMethod]
    public void BulkOperationDto_StoresNodeIdsAndTarget()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var targetId = Guid.NewGuid();

        var dto = new BulkOperationDto { NodeIds = ids, TargetParentId = targetId };

        Assert.AreEqual(2, dto.NodeIds.Count);
        Assert.AreEqual(targetId, dto.TargetParentId);
    }

    [TestMethod]
    public void BulkOperationDto_TargetParentIdIsOptional()
    {
        var dto = new BulkOperationDto { NodeIds = [Guid.NewGuid()] };

        Assert.IsNull(dto.TargetParentId);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public async Task BulkMove_EmptyList_ReturnsEmptyResults()
    {
        using var db = CreateContext();
        var service = CreateFileService(db);
        var caller = UserCaller(Guid.NewGuid());
        var target = Guid.NewGuid();

        var results = await ExecuteBulkAsync([], id =>
            service.MoveAsync(id, new MoveNodeDto { TargetParentId = target }, caller));

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task BulkDelete_EmptyList_ReturnsEmptyResults()
    {
        using var db = CreateContext();
        var service = CreateFileService(db);
        var caller = UserCaller(Guid.NewGuid());

        var results = await ExecuteBulkAsync([], id =>
            service.DeleteAsync(id, caller));

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task BulkDelete_AllFail_AllMarkedAsFailed()
    {
        using var db = CreateContext();
        var service = CreateFileService(db);
        var caller = UserCaller(Guid.NewGuid());
        var nodeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var results = await ExecuteBulkAsync(nodeIds, id =>
            service.DeleteAsync(id, caller));

        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.All(r => !r.Success));
        Assert.IsTrue(results.All(r => !string.IsNullOrEmpty(r.Error)));
    }

    #endregion
}
