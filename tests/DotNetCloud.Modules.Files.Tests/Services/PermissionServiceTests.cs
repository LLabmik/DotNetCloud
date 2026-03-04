using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class PermissionServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PermissionService CreateService(FilesDbContext db) => new(db);

    private static CallerContext UserCaller(Guid userId) =>
        new(userId, Array.Empty<string>(), CallerType.User);

    private static FileNode CreateNode(Guid ownerId, FileNodeType type = FileNodeType.File,
        Guid? parentId = null, string? materializedPath = null)
    {
        var node = new FileNode
        {
            Name = "node",
            NodeType = type,
            OwnerId = ownerId,
            ParentId = parentId
        };
        node.MaterializedPath = materializedPath ?? $"/{node.Id}";
        return node;
    }

    // ── Ownership ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetEffectivePermissionAsync_Owner_ReturnsFullPermission()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEffectivePermissionAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(SharePermission.Full, result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_SystemCaller_ReturnsFullPermission()
    {
        using var db = CreateContext();
        var node = CreateNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, CallerContext.CreateSystemContext());

        Assert.AreEqual(SharePermission.Full, result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_NonOwnerNoShare_ReturnsNull()
    {
        using var db = CreateContext();
        var node = CreateNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_NodeNotFound_ReturnsNull()
    {
        using var db = CreateContext();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }

    // ── Direct user shares ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetEffectivePermissionAsync_ActiveUserShare_ReturnsGrantedPermission()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.ReadWrite,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, UserCaller(targetUserId));

        Assert.AreEqual(SharePermission.ReadWrite, result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_ExpiredShare_ReturnsNull()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.ReadWrite,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, UserCaller(targetUserId));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_PublicLinkShare_NotCountedForUserPermission()
    {
        // Public link shares should not grant user-based permission checks.
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Read,
            LinkToken = "tok",
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, UserCaller(targetUserId));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_MultipleShares_ReturnsMostPermissive()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Full,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(node.Id, UserCaller(targetUserId));

        Assert.AreEqual(SharePermission.Full, result);
    }

    // ── Cascading shares ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetEffectivePermissionAsync_ParentFolderShared_ChildInheritsPermission()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        // Parent folder
        var parent = CreateNode(ownerId, FileNodeType.Folder);
        db.FileNodes.Add(parent);

        // Child file under parent
        var child = CreateNode(ownerId, FileNodeType.File, parentId: parent.Id,
            materializedPath: $"{parent.MaterializedPath}/{Guid.NewGuid()}");
        db.FileNodes.Add(child);

        // Share is on the parent, not the child.
        db.FileShares.Add(new FileShare
        {
            FileNodeId = parent.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.ReadWrite,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(child.Id, UserCaller(targetUserId));

        Assert.AreEqual(SharePermission.ReadWrite, result);
    }

    [TestMethod]
    public async Task GetEffectivePermissionAsync_GrandparentShared_DeepChildInherits()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var grandparent = CreateNode(ownerId, FileNodeType.Folder);
        grandparent.MaterializedPath = $"/{grandparent.Id}";
        db.FileNodes.Add(grandparent);

        var parent = CreateNode(ownerId, FileNodeType.Folder, parentId: grandparent.Id);
        parent.MaterializedPath = $"{grandparent.MaterializedPath}/{parent.Id}";
        db.FileNodes.Add(parent);

        var child = CreateNode(ownerId, FileNodeType.File, parentId: parent.Id);
        child.MaterializedPath = $"{parent.MaterializedPath}/{child.Id}";
        db.FileNodes.Add(child);

        db.FileShares.Add(new FileShare
        {
            FileNodeId = grandparent.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .GetEffectivePermissionAsync(child.Id, UserCaller(targetUserId));

        Assert.AreEqual(SharePermission.Read, result);
    }

    // ── HasPermissionAsync / RequirePermissionAsync ─────────────────────────

    [TestMethod]
    public async Task HasPermissionAsync_ReadSatisfiesRead_ReturnsTrue()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .HasPermissionAsync(node.Id, UserCaller(targetUserId), SharePermission.Read);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasPermissionAsync_ReadDoesNotSatisfyReadWrite_ReturnsFalse()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var node = CreateNode(ownerId);
        db.FileNodes.Add(node);
        db.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.User,
            SharedWithUserId = targetUserId,
            Permission = SharePermission.Read,
            CreatedByUserId = ownerId
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db)
            .HasPermissionAsync(node.Id, UserCaller(targetUserId), SharePermission.ReadWrite);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RequirePermissionAsync_InsufficientPermission_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var node = CreateNode(Guid.NewGuid());
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => CreateService(db).RequirePermissionAsync(node.Id, UserCaller(Guid.NewGuid()), SharePermission.Read));
    }

    [TestMethod]
    public async Task RequirePermissionAsync_SufficientPermission_DoesNotThrow()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = CreateNode(userId);
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        // Owner should never throw.
        await CreateService(db).RequirePermissionAsync(node.Id, UserCaller(userId), SharePermission.Full);
    }
}
