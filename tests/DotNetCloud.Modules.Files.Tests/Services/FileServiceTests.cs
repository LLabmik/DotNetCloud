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
using FilesFileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class FileServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FileService CreateService(FilesDbContext db, IQuotaService? quotaService = null, FileSystemOptions? fileSystemOptions = null) =>
        new(db, Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<FileService>(), new PermissionService(db), new DeviceContext(), quotaService ?? Mock.Of<IQuotaService>(),
            Microsoft.Extensions.Options.Options.Create(fileSystemOptions ?? new FileSystemOptions()),
            Mock.Of<ISyncChangeNotifier>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task CreateFolderAsync_AtRoot_CreatesSuccessfully()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var result = await service.CreateFolderAsync(
            new CreateFolderDto { Name = "MyFolder" },
            UserCaller(userId));

        Assert.AreEqual("MyFolder", result.Name);
        Assert.AreEqual("Folder", result.NodeType);
        Assert.IsNull(result.ParentId);
        Assert.AreEqual(userId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateFolderAsync_UnderParent_SetsPathAndDepth()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode
        {
            Name = "Parent",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/parent-id",
            Depth = 0
        };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateFolderAsync(
            new CreateFolderDto { Name = "Child", ParentId = parent.Id },
            UserCaller(userId));

        Assert.AreEqual(parent.Id, result.ParentId);
        Assert.AreEqual("Child", result.Name);
    }

    [TestMethod]
    public async Task CreateFolderAsync_DuplicateName_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "Existing", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CreateFolderAsync(
                new CreateFolderDto { Name = "Existing" },
                UserCaller(userId)));
    }

    [TestMethod]
    public async Task GetNodeAsync_ExistingNode_ReturnsDto()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetNodeAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("file.txt", result.Name);
    }

    [TestMethod]
    public async Task GetNodeAsync_NonExistent_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetNodeAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListChildrenAsync_ReturnsChildrenSorted()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Root", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(parent);
        db.FileNodes.Add(new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id });
        db.FileNodes.Add(new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id });
        db.FileNodes.Add(new FileNode { Name = "SubFolder", NodeType = FileNodeType.Folder, OwnerId = userId, ParentId = parent.Id });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var children = await service.ListChildrenAsync(parent.Id, UserCaller(userId));

        Assert.AreEqual(3, children.Count);
        // Files (enum 0) come first sorted by name, then folders (enum 1)
        Assert.AreEqual("a.txt", children[0].Name);
        Assert.AreEqual("b.txt", children[1].Name);
        Assert.AreEqual("SubFolder", children[2].Name);
    }

    [TestMethod]
    public async Task ListChildrenAsync_NodeWithMultipleTags_ReturnsUniqueNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Root", NodeType = FileNodeType.Folder, OwnerId = userId };
        var child = new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id };

        db.FileNodes.AddRange(parent, child);
        db.FileTags.AddRange(
            new FileTag { FileNodeId = child.Id, Name = "Work", Color = "#112233", CreatedByUserId = userId },
            new FileTag { FileNodeId = child.Id, Name = "Important", Color = "#445566", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var children = await service.ListChildrenAsync(parent.Id, UserCaller(userId));

        Assert.AreEqual(1, children.Count);
        Assert.AreEqual("report.pdf", children[0].Name);
        Assert.AreEqual(2, children[0].Tags.Count);
    }

    [TestMethod]
    public async Task ListRootAsync_NodeWithMultipleTags_ReturnsUniqueNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = userId };

        db.FileNodes.Add(node);
        db.FileTags.AddRange(
            new FileTag { FileNodeId = node.Id, Name = "Work", Color = "#112233", CreatedByUserId = userId },
            new FileTag { FileNodeId = node.Id, Name = "Important", Color = "#445566", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var roots = await service.ListRootAsync(UserCaller(userId));

        Assert.AreEqual(1, roots.Count);
        Assert.AreEqual("report.pdf", roots[0].Name);
        Assert.AreEqual(2, roots[0].Tags.Count);
    }

    [TestMethod]
    public async Task RenameAsync_ValidInput_UpdatesName()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "old.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RenameAsync(node.Id, new RenameNodeDto { Name = "new.txt" }, UserCaller(userId));

        Assert.AreEqual("new.txt", result.Name);
    }

    [TestMethod]
    public async Task RenameAsync_NonOwner_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => service.RenameAsync(node.Id, new RenameNodeDto { Name = "hacked.txt" }, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task MoveAsync_ValidMove_UpdatesParent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var source = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            MaterializedPath = "/file"
        };
        source.MaterializedPath = $"/{source.Id}";
        var target = new FileNode
        {
            Name = "Target",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            Depth = 0
        };
        target.MaterializedPath = $"/{target.Id}";
        db.FileNodes.AddRange(source, target);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.MoveAsync(source.Id, new MoveNodeDto { TargetParentId = target.Id }, UserCaller(userId));

        Assert.AreEqual(target.Id, result.ParentId);
    }

    [TestMethod]
    public async Task MoveAsync_IntoSelf_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        folder.MaterializedPath = $"/{folder.Id}";
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.MoveAsync(folder.Id, new MoveNodeDto { TargetParentId = folder.Id }, UserCaller(userId)));
    }

    [TestMethod]
    public async Task DeleteAsync_SoftDeletesNode()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        node.MaterializedPath = $"/{node.Id}";
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteAsync(node.Id, UserCaller(userId));

        // Query filters should hide it
        Assert.AreEqual(0, await db.FileNodes.CountAsync());

        // But it exists with IgnoreQueryFilters
        var deleted = await db.FileNodes.IgnoreQueryFilters().FirstAsync(n => n.Id == node.Id);
        Assert.IsTrue(deleted.IsDeleted);
        Assert.IsNotNull(deleted.DeletedAt);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesSharesWhenTrashing()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "shared.txt", NodeType = FileNodeType.File, OwnerId = userId };
        node.MaterializedPath = $"/{node.Id}";
        db.FileNodes.Add(node);

        // Add a share for the node
        db.FileShares.Add(new FilesFileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            Permission = SharePermission.Read,
            CreatedByUserId = userId,
            SharedWithUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteAsync(node.Id, UserCaller(userId));

        // Share should be removed
        Assert.AreEqual(0, await db.FileShares.CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_FolderWithSharedDescendants_RemovesAllShares()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        folder.MaterializedPath = $"/{folder.Id}";
        db.FileNodes.Add(folder);

        var child = new FileNode { Name = "child.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = folder.Id, Depth = 1 };
        child.MaterializedPath = $"{folder.MaterializedPath}/{child.Id}";
        db.FileNodes.Add(child);

        db.FileShares.Add(new FilesFileShare
        {
            FileNodeId = folder.Id,
            ShareType = ShareType.User,
            Permission = SharePermission.Read,
            CreatedByUserId = userId,
            SharedWithUserId = Guid.NewGuid()
        });
        db.FileShares.Add(new FilesFileShare
        {
            FileNodeId = child.Id,
            ShareType = ShareType.User,
            Permission = SharePermission.Read,
            CreatedByUserId = userId,
            SharedWithUserId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteAsync(folder.Id, UserCaller(userId));

        // Both shares should be removed
        Assert.AreEqual(0, await db.FileShares.CountAsync());
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_TogglesState()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result1 = await service.ToggleFavoriteAsync(node.Id, UserCaller(userId));
        Assert.IsTrue(result1.IsFavorite);

        var result2 = await service.ToggleFavoriteAsync(node.Id, UserCaller(userId));
        Assert.IsFalse(result2.IsFavorite);
    }

    [TestMethod]
    public async Task SearchAsync_ByName_ReturnsMatches()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "report-final.pdf", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "notes.txt", NodeType = FileNodeType.File, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.SearchAsync("report", 1, 10, UserCaller(userId));

        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(2, result.Items.Count);
    }

    [TestMethod]
    public async Task CopyAsync_File_CreatesCopy()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var source = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 100,
            MimeType = "text/plain",
            ContentHash = "abc123"
        };
        source.MaterializedPath = $"/{source.Id}";
        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";
        db.FileNodes.AddRange(source, target);
        await db.SaveChangesAsync();

        var quotaMock = new Mock<IQuotaService>();
        quotaMock.Setup(q => q.HasSufficientQuotaAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var service = CreateService(db, quotaMock.Object);
        var result = await service.CopyAsync(source.Id, target.Id, UserCaller(userId));

        Assert.AreNotEqual(source.Id, result.Id);
        Assert.AreEqual("file.txt", result.Name);
        Assert.AreEqual(target.Id, result.ParentId);
        Assert.AreEqual(100, result.Size);
    }

    [TestMethod]
    public async Task CopyAsync_InsufficientQuota_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var source = new FileNode { Name = "big.dat", NodeType = FileNodeType.File, OwnerId = userId, Size = 5000 };
        source.MaterializedPath = $"/{source.Id}";
        var target = new FileNode { Name = "Target", NodeType = FileNodeType.Folder, OwnerId = userId, Depth = 0 };
        target.MaterializedPath = $"/{target.Id}";
        db.FileNodes.AddRange(source, target);
        await db.SaveChangesAsync();

        var quotaMock = new Mock<IQuotaService>();
        quotaMock.Setup(q => q.HasSufficientQuotaAsync(userId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var service = CreateService(db, quotaMock.Object);
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CopyAsync(source.Id, target.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task ListRecentAsync_ReturnsRecentFilesOnly()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "old.txt", NodeType = FileNodeType.File, OwnerId = userId, UpdatedAt = DateTime.UtcNow.AddDays(-10) });
        db.FileNodes.Add(new FileNode { Name = "new.txt", NodeType = FileNodeType.File, OwnerId = userId, UpdatedAt = DateTime.UtcNow });
        db.FileNodes.Add(new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ListRecentAsync(10, UserCaller(userId));

        Assert.AreEqual(2, result.Count); // Only files, not folders
        Assert.AreEqual("new.txt", result[0].Name); // Most recent first
    }

    [TestMethod]
    public async Task ListRecentAsync_RespectsCountLimit()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            db.FileNodes.Add(new FileNode
            {
                Name = $"file{i}.txt",
                NodeType = FileNodeType.File,
                OwnerId = userId,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ListRecentAsync(3, UserCaller(userId));

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public async Task ListRecentAsync_OtherUsersFiles_NotIncluded()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "mine.txt", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "theirs.txt", NodeType = FileNodeType.File, OwnerId = otherId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ListRecentAsync(10, UserCaller(userId));

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mine.txt", result[0].Name);
    }

    [TestMethod]
    public async Task CreateFolderAsync_CaseInsensitiveDuplicate_ThrowsNameConflictException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "Documents", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.NameConflictException>(
            () => service.CreateFolderAsync(
                new CreateFolderDto { Name = "documents" },
                UserCaller(userId)));
    }

    [TestMethod]
    public async Task CreateFolderAsync_CaseInsensitiveDuplicate_Disabled_Succeeds()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "Documents", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db, fileSystemOptions: new FileSystemOptions { EnforceCaseInsensitiveUniqueness = false });
        var result = await service.CreateFolderAsync(
            new CreateFolderDto { Name = "documents" },
            UserCaller(userId));

        Assert.AreEqual("documents", result.Name);
    }

    [TestMethod]
    public async Task RenameAsync_CaseInsensitiveDuplicate_ThrowsNameConflictException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "Report.txt", NodeType = FileNodeType.File, OwnerId = userId });
        var target = new FileNode { Name = "notes.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(target);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.NameConflictException>(
            () => service.RenameAsync(target.Id, new RenameNodeDto { Name = "REPORT.TXT" }, UserCaller(userId)));
    }

    [TestMethod]
    public async Task RenameAsync_SameNameDifferentCase_Self_Succeeds()
    {
        // Renaming "file.txt" to "File.txt" — the only case-variant is itself (excluded by ID), should not throw.
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RenameAsync(node.Id, new RenameNodeDto { Name = "File.txt" }, UserCaller(userId));

        Assert.AreEqual("File.txt", result.Name);
    }

    // ---- Task 4.5: Filename compatibility validation ----

    [TestMethod]
    public void ValidateFilenameCompatibility_IllegalChar_ThrowsValidationException()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = true };
        var ex = Assert.ThrowsExactly<Core.Errors.ValidationException>(
            () => FileService.ValidateFilenameCompatibility("bad:name.txt", options));
        StringAssert.Contains(ex.Message, ":");
    }

    [TestMethod]
    public void ValidateFilenameCompatibility_ReservedName_ThrowsValidationException()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = true };
        Assert.ThrowsExactly<Core.Errors.ValidationException>(
            () => FileService.ValidateFilenameCompatibility("CON.txt", options));
    }

    [TestMethod]
    public void ValidateFilenameCompatibility_ReservedNameCaseInsensitive_ThrowsValidationException()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = true };
        Assert.ThrowsExactly<Core.Errors.ValidationException>(
            () => FileService.ValidateFilenameCompatibility("nul", options));
    }

    [TestMethod]
    public void ValidateFilenameCompatibility_ValidName_DoesNotThrow()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = true };
        FileService.ValidateFilenameCompatibility("my-document (2024).pdf", options);
    }

    [TestMethod]
    public void ValidateFilenameCompatibility_WhenDisabled_AllowsReservedName()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = false };
        FileService.ValidateFilenameCompatibility("CON", options);
    }

    [TestMethod]
    public void ValidateFilenameCompatibility_WhenDisabled_AllowsIllegalChar()
    {
        var options = new FileSystemOptions { EnforceWindowsFilenameCompatibility = false };
        FileService.ValidateFilenameCompatibility("file:name.txt", options);
    }

    [TestMethod]
    public async Task CreateFolderAsync_WindowsIllegalName_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var service = CreateService(db, fileSystemOptions: new FileSystemOptions
        {
            EnforceWindowsFilenameCompatibility = true
        });

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CreateFolderAsync(new CreateFolderDto { Name = "folder<name>" }, UserCaller(userId)));
    }

    [TestMethod]
    public async Task RenameAsync_WindowsReservedName_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "notes.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db, fileSystemOptions: new FileSystemOptions
        {
            EnforceWindowsFilenameCompatibility = true
        });

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.RenameAsync(node.Id, new RenameNodeDto { Name = "AUX" }, UserCaller(userId)));
    }
}
