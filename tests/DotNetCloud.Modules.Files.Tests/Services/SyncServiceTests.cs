using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class SyncServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static SyncService CreateService(FilesDbContext db, Guid? deviceId = null) =>
        new(
            db,
            NullLoggerFactory.Instance.CreateLogger<SyncService>(),
            new DeviceContext { DeviceId = deviceId });

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task GetChangesSinceAsync_ReturnsActiveChanges()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.FileNodes.Add(new FileNode
        {
            Name = "recent.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now
        });
        db.FileNodes.Add(new FileNode
        {
            Name = "old.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now.AddDays(-10)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("recent.txt", changes[0].Name);
        Assert.IsFalse(changes[0].IsDeleted);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_IncludesDeletedNodes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = now,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid(),
            UpdatedAt = now.AddMinutes(-5)
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.IsTrue(changes[0].IsDeleted);
        Assert.AreEqual("deleted.txt", changes[0].Name);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_FiltersByFolder()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId, UpdatedAt = now };
        folder.MaterializedPath = $"/{folder.Id}";
        db.FileNodes.Add(folder);

        db.FileNodes.Add(new FileNode
        {
            Name = "inside.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ParentId = folder.Id,
            UpdatedAt = now
        });
        db.FileNodes.Add(new FileNode
        {
            Name = "outside.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), folder.Id, UserCaller(userId));

        // Should include the folder itself and its child, but not the outside file
        Assert.IsTrue(changes.All(c => c.Name != "outside.txt"));
        Assert.IsTrue(changes.Any(c => c.Name == "inside.txt"));
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_OtherUsersNodes_NotIncluded()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.FileNodes.Add(new FileNode { Name = "mine.txt", NodeType = FileNodeType.File, OwnerId = userId, UpdatedAt = now });
        db.FileNodes.Add(new FileNode { Name = "theirs.txt", NodeType = FileNodeType.File, OwnerId = otherId, UpdatedAt = now });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("mine.txt", changes[0].Name);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_SelfOriginatedChanges_AreSuppressed()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var selfDeviceId = Guid.NewGuid();
        var otherDeviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.FileNodes.Add(new FileNode
        {
            Name = "self.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now,
            OriginatingDeviceId = selfDeviceId
        });
        db.FileNodes.Add(new FileNode
        {
            Name = "other.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            UpdatedAt = now,
            OriginatingDeviceId = otherDeviceId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, selfDeviceId);
        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("other.txt", changes[0].Name);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_RootLevel_ReturnsAllRootNodes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileNodes.Add(new FileNode { Name = "file1.txt", NodeType = FileNodeType.File, OwnerId = userId });
        db.FileNodes.Add(new FileNode { Name = "Folder1", NodeType = FileNodeType.Folder, OwnerId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tree = await service.GetFolderTreeAsync(null, UserCaller(userId));

        Assert.AreEqual("/", tree.Name);
        Assert.AreEqual(2, tree.Children.Count);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_SpecificFolder_BuildsRecursiveTree()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var parent = new FileNode { Name = "Root", NodeType = FileNodeType.Folder, OwnerId = userId };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);

        var child = new FileNode { Name = "child.txt", NodeType = FileNodeType.File, OwnerId = userId, ParentId = parent.Id };
        db.FileNodes.Add(child);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var tree = await service.GetFolderTreeAsync(parent.Id, UserCaller(userId));

        Assert.AreEqual("Root", tree.Name);
        Assert.AreEqual(1, tree.Children.Count);
        Assert.AreEqual("child.txt", tree.Children[0].Name);
    }

    [TestMethod]
    public async Task GetFolderTreeAsync_NonExistentFolder_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetFolderTreeAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task ReconcileAsync_NewOnServer_ProducesDownloadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var serverNode = new FileNode
        {
            Name = "server-only.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "abc"
        };
        db.FileNodes.Add(serverNode);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [] // Client has nothing
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Download", result.Actions[0].Action);
        Assert.AreEqual(serverNode.Id, result.Actions[0].NodeId);
    }

    [TestMethod]
    public async Task ReconcileAsync_NewOnClient_ProducesUploadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        // Server is empty
        await db.SaveChangesAsync();

        var clientNodeId = Guid.NewGuid();
        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = clientNodeId, ContentHash = "xyz", UpdatedAt = DateTime.UtcNow }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Upload", result.Actions[0].Action);
        Assert.AreEqual(clientNodeId, result.Actions[0].NodeId);
    }

    [TestMethod]
    public async Task ReconcileAsync_DeletedOnServer_ProducesDeleteAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid()
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = deleted.Id, ContentHash = "old", UpdatedAt = DateTime.UtcNow.AddDays(-1) }]
        }, UserCaller(userId));

        Assert.IsTrue(result.Actions.Any(a => a.Action == "Delete" && a.NodeId == deleted.Id));
    }

    [TestMethod]
    public async Task ReconcileAsync_ServerNewer_ProducesDownloadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "client_hash", UpdatedAt = now.AddMinutes(-5) }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Download", result.Actions[0].Action);
        Assert.AreEqual("Server is newer", result.Actions[0].Reason);
    }

    [TestMethod]
    public async Task ReconcileAsync_ClientNewer_ProducesUploadAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now.AddMinutes(-5)
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "client_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Upload", result.Actions[0].Action);
        Assert.AreEqual("Client is newer", result.Actions[0].Reason);
    }

    [TestMethod]
    public async Task ReconcileAsync_SameHashAndTimestamp_NoAction()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "same_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "same_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(0, result.Actions.Count);
    }

    [TestMethod]
    public async Task ReconcileAsync_SameTimestampDifferentHash_ProducesConflict()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "server_hash",
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ReconcileAsync(new SyncReconcileRequestDto
        {
            ClientNodes = [new SyncClientNodeDto { NodeId = node.Id, ContentHash = "different_hash", UpdatedAt = now }]
        }, UserCaller(userId));

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("Conflict", result.Actions[0].Action);
    }

    // ── Cursor-based delta sync (Tasks 2.4 + 2.5) ────────────────────────────

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_NoCursor_ReturnsAllStampedChanges()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // Node with a SyncSequence (stamped mutation)
        var stamped = new FileNode
        {
            Name = "stamped.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            SyncSequence = 1
        };
        // Node without SyncSequence (legacy, not yet mutated)
        var legacy = new FileNode
        {
            Name = "legacy.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            SyncSequence = null
        };
        db.FileNodes.AddRange(stamped, legacy);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(null, null, 500, UserCaller(userId));

        Assert.AreEqual(1, result.Changes.Count);
        Assert.AreEqual("stamped.txt", result.Changes[0].Name);
        Assert.IsFalse(result.HasMore);
        Assert.IsNotNull(result.NextCursor);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_WithCursor_ReturnsOnlyNewerChanges()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        db.FileNodes.Add(new FileNode { Name = "seq1.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = 1 });
        db.FileNodes.Add(new FileNode { Name = "seq2.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = 2 });
        db.FileNodes.Add(new FileNode { Name = "seq3.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = 3 });
        await db.SaveChangesAsync();

        var cursor = SyncCursorHelper.EncodeCursor(userId, 1); // "I've seen up to sequence 1"

        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(cursor, null, 500, UserCaller(userId));

        Assert.AreEqual(2, result.Changes.Count);
        Assert.IsTrue(result.Changes.All(c => c.SyncSequence > 1));
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_WithLimit_PaginatesCorrectly()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        for (long seq = 1; seq <= 5; seq++)
            db.FileNodes.Add(new FileNode { Name = $"f{seq}.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = seq });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(null, null, 3, UserCaller(userId));

        Assert.AreEqual(3, result.Changes.Count);
        Assert.IsTrue(result.HasMore);
        Assert.IsNotNull(result.NextCursor);

        // NextCursor decoded should point to sequence 3 (the last item in the page)
        var decoded = SyncCursorHelper.DecodeCursor(result.NextCursor!);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(userId, decoded!.Value.UserId);
        Assert.AreEqual(3L, decoded.Value.Sequence);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_InvalidCursor_StartsFromBeginning()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        db.FileNodes.Add(new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = 1 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        // Garbage input — should fall back to sinceSequence=0
        var result = await service.GetChangesSinceCursorAsync("not-valid-base64!!", null, 500, UserCaller(userId));

        Assert.AreEqual(1, result.Changes.Count);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_CursorFromDifferentUser_StartsFromBeginning()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.FileNodes.Add(new FileNode { Name = "mine.txt", NodeType = FileNodeType.File, OwnerId = userId, SyncSequence = 1 });
        await db.SaveChangesAsync();

        // Cursor encoded for a different user
        var foreignCursor = SyncCursorHelper.EncodeCursor(otherUserId, 999);

        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(foreignCursor, null, 500, UserCaller(userId));

        // Falls back to seq 0 — returns the file
        Assert.AreEqual(1, result.Changes.Count);
        Assert.AreEqual("mine.txt", result.Changes[0].Name);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_DeletedNodeWithSequence_IncludedInResults()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var deleted = new FileNode
        {
            Name = "deleted.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = userId,
            OriginalParentId = Guid.NewGuid(),
            SyncSequence = 2
        };
        deleted.MaterializedPath = $"/{deleted.Id}";
        db.FileNodes.Add(deleted);
        await db.SaveChangesAsync();

        var cursor = SyncCursorHelper.EncodeCursor(userId, 1);
        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(cursor, null, 500, UserCaller(userId));

        Assert.AreEqual(1, result.Changes.Count);
        Assert.IsTrue(result.Changes[0].IsDeleted);
        Assert.AreEqual("deleted.txt", result.Changes[0].Name);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_EmptyResult_NextCursorEncodesSinceSequence()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var cursor = SyncCursorHelper.EncodeCursor(userId, 42);
        var service = CreateService(db);
        var result = await service.GetChangesSinceCursorAsync(cursor, null, 500, UserCaller(userId));

        Assert.AreEqual(0, result.Changes.Count);
        Assert.IsFalse(result.HasMore);
        // When no items found, cursor stays at sinceSequence (42)
        var decoded = SyncCursorHelper.DecodeCursor(result.NextCursor!);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(42L, decoded!.Value.Sequence);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_SelfOriginatedChange_FilteredButCursorAdvances()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var selfDeviceId = Guid.NewGuid();

        db.FileNodes.Add(new FileNode
        {
            Name = "self-only.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            SyncSequence = 1,
            OriginatingDeviceId = selfDeviceId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, selfDeviceId);
        var result = await service.GetChangesSinceCursorAsync(null, null, 500, UserCaller(userId));

        Assert.AreEqual(0, result.Changes.Count);
        Assert.IsFalse(result.HasMore);

        var decoded = SyncCursorHelper.DecodeCursor(result.NextCursor!);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(1L, decoded!.Value.Sequence);
    }

    // ── SyncCursorHelper unit tests ───────────────────────────────────────────

    [TestMethod]
    public void SyncCursorHelper_EncodeDecode_RoundTrip()
    {
        var userId = Guid.NewGuid();
        const long seq = 12345L;

        var cursor = SyncCursorHelper.EncodeCursor(userId, seq);
        var decoded = SyncCursorHelper.DecodeCursor(cursor);

        Assert.IsNotNull(decoded);
        Assert.AreEqual(userId, decoded!.Value.UserId);
        Assert.AreEqual(seq, decoded.Value.Sequence);
    }

    [TestMethod]
    public void SyncCursorHelper_DecodeInvalidBase64_ReturnsNull()
    {
        var result = SyncCursorHelper.DecodeCursor("!!!not-base64!!!");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SyncCursorHelper_DecodeValidBase64ButBadFormat_ReturnsNull()
    {
        // Valid base64 but not "guid:long" format
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("notavalidformat"));
        var result = SyncCursorHelper.DecodeCursor(raw);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SyncCursorHelper_DecodeNoColon_ReturnsNull()
    {
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        var result = SyncCursorHelper.DecodeCursor(raw);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SyncCursorHelper_AssignNextSequenceAsync_CreatesCounterAndAssignsSequence()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "f.txt", NodeType = FileNodeType.File, OwnerId = userId };

        await SyncCursorHelper.AssignNextSequenceAsync(db, node, userId);

        Assert.AreEqual(1L, node.SyncSequence);
    }

    [TestMethod]
    public async Task SyncCursorHelper_AssignNextSequenceAsync_IncrementsExistingCounter()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // Pre-seed counter at 10
        db.UserSyncCounters.Add(new UserSyncCounter { UserId = userId, CurrentSequence = 10 });
        await db.SaveChangesAsync();

        var node = new FileNode { Name = "f.txt", NodeType = FileNodeType.File, OwnerId = userId };
        await SyncCursorHelper.AssignNextSequenceAsync(db, node, userId);

        Assert.AreEqual(11L, node.SyncSequence);
    }

    [TestMethod]
    public async Task SyncCursorHelper_AssignNextSequenceAsync_MultipleNodes_SequenceMonotonicallyIncreases()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node1 = new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId };
        var node2 = new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId };
        var node3 = new FileNode { Name = "c.txt", NodeType = FileNodeType.File, OwnerId = userId };

        await SyncCursorHelper.AssignNextSequenceAsync(db, node1, userId);
        await SyncCursorHelper.AssignNextSequenceAsync(db, node2, userId);
        await SyncCursorHelper.AssignNextSequenceAsync(db, node3, userId);

        Assert.AreEqual(1L, node1.SyncSequence);
        Assert.AreEqual(2L, node2.SyncSequence);
        Assert.AreEqual(3L, node3.SyncSequence);
    }

    // --- Device identity / echo suppression data flow tests ---

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_IncludesOriginatingDeviceId()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var service = CreateService(db);

        var node = new FileNode
        {
            Id = Guid.NewGuid(),
            Name = "device-file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "abc123",
            Size = 100,
            OriginatingDeviceId = deviceId,
            SyncSequence = 1
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await service.GetChangesSinceCursorAsync(null, null, 100, UserCaller(userId));

        Assert.AreEqual(1, result.Changes.Count);
        Assert.AreEqual(deviceId, result.Changes[0].OriginatingDeviceId);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_IncludesOriginatingDeviceId()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var service = CreateService(db);

        var node = new FileNode
        {
            Id = Guid.NewGuid(),
            Name = "device-file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "abc123",
            Size = 100,
            OriginatingDeviceId = deviceId,
            UpdatedAt = now
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var changes = await service.GetChangesSinceAsync(now.AddMinutes(-1), null, UserCaller(userId));

        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(deviceId, changes[0].OriginatingDeviceId);
    }

    [TestMethod]
    public async Task GetChangesSinceCursorAsync_NullOriginatingDeviceId_IsPreserved()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var node = new FileNode
        {
            Id = Guid.NewGuid(),
            Name = "no-device-file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ContentHash = "abc123",
            Size = 100,
            OriginatingDeviceId = null,
            SyncSequence = 1
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var result = await service.GetChangesSinceCursorAsync(null, null, 100, UserCaller(userId));

        Assert.AreEqual(1, result.Changes.Count);
        Assert.IsNull(result.Changes[0].OriginatingDeviceId);
    }

    // ── P2.2: Per-Device Cursor Tracking ─────────────────────────────────────

    [TestMethod]
    public async Task AcknowledgeCursorAsync_CreatesNewCursor()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice
        {
            Id = deviceId,
            UserId = userId,
            DeviceName = "test-device"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        await service.AcknowledgeCursorAsync(userId, deviceId, 10);

        var cursor = await db.SyncDeviceCursors.FindAsync(deviceId);
        Assert.IsNotNull(cursor);
        Assert.AreEqual(10L, cursor!.LastAcknowledgedSequence);
        Assert.AreEqual(userId, cursor.UserId);
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_AdvancesForward()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        await service.AcknowledgeCursorAsync(userId, deviceId, 5);
        await service.AcknowledgeCursorAsync(userId, deviceId, 15);

        var cursor = await db.SyncDeviceCursors.FindAsync(deviceId);
        Assert.AreEqual(15L, cursor!.LastAcknowledgedSequence);
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_DoesNotRegress()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        await service.AcknowledgeCursorAsync(userId, deviceId, 20);
        await service.AcknowledgeCursorAsync(userId, deviceId, 5); // Lower value — should be ignored

        var cursor = await db.SyncDeviceCursors.FindAsync(deviceId);
        Assert.AreEqual(20L, cursor!.LastAcknowledgedSequence,
            "Cursor should not regress to a lower sequence number.");
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_WrongUser_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.AcknowledgeCursorAsync(otherUserId, deviceId, 10));
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_NegativeSequence_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.AcknowledgeCursorAsync(userId, deviceId, -1));
    }

    [TestMethod]
    public async Task GetDeviceCursorAsync_NoCursorYet_ReturnsNullSequence()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        var result = await service.GetDeviceCursorAsync(userId, deviceId);

        Assert.IsNotNull(result);
        Assert.AreEqual(deviceId, result.DeviceId);
        Assert.IsNull(result.LastAcknowledgedSequence);
        Assert.IsNull(result.Cursor);
        Assert.IsNull(result.UpdatedAt);
    }

    [TestMethod]
    public async Task GetDeviceCursorAsync_WithCursor_ReturnsEncodedCursor()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        db.SyncDeviceCursors.Add(new SyncDeviceCursor
        {
            DeviceId = deviceId,
            UserId = userId,
            LastAcknowledgedSequence = 42,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        var result = await service.GetDeviceCursorAsync(userId, deviceId);

        Assert.IsNotNull(result);
        Assert.AreEqual(42L, result.LastAcknowledgedSequence);
        Assert.IsNotNull(result.Cursor);

        // Verify the cursor decodes correctly
        var decoded = SyncCursorHelper.DecodeCursor(result.Cursor!);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(userId, decoded!.Value.UserId);
        Assert.AreEqual(42L, decoded.Value.Sequence);
    }

    [TestMethod]
    public async Task GetDeviceCursorAsync_WrongUser_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetDeviceCursorAsync(otherUserId, deviceId));
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_ThenGet_RoundTrip()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.SyncDevices.Add(new SyncDevice { Id = deviceId, UserId = userId, DeviceName = "test" });
        await db.SaveChangesAsync();

        var service = CreateService(db, deviceId);
        await service.AcknowledgeCursorAsync(userId, deviceId, 99);

        var result = await service.GetDeviceCursorAsync(userId, deviceId);
        Assert.AreEqual(99L, result.LastAcknowledgedSequence);
        Assert.IsNotNull(result.Cursor);
        Assert.IsNotNull(result.UpdatedAt);
    }

    [TestMethod]
    public async Task AcknowledgeCursorAsync_NonExistentDevice_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var nonExistentDeviceId = Guid.NewGuid();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.AcknowledgeCursorAsync(userId, nonExistentDeviceId, 10));
    }
}
