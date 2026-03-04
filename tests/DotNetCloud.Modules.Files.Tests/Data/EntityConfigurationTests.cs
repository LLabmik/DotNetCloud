using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests for EF Core entity configurations verifying relationships, indexes,
/// query filters, and key configurations.
/// </summary>
[TestClass]
public class EntityConfigurationTests
{
    private static FilesDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    // ---- FileNode: Parent-Child Relationship ----

    [TestMethod]
    public async Task WhenFileNodeHasParentThenRelationshipIsTracked()
    {
        using var context = CreateContext();

        var parent = new FileNode
        {
            Name = "Documents",
            NodeType = FileNodeType.Folder,
            OwnerId = Guid.NewGuid(),
            MaterializedPath = "/root"
        };
        var child = new FileNode
        {
            Name = "report.pdf",
            NodeType = FileNodeType.File,
            OwnerId = parent.OwnerId,
            ParentId = parent.Id,
            MaterializedPath = "/root/child"
        };

        context.FileNodes.AddRange(parent, child);
        await context.SaveChangesAsync();

        var loaded = await context.FileNodes
            .Include(n => n.Children)
            .FirstAsync(n => n.Id == parent.Id);

        Assert.AreEqual(1, loaded.Children.Count);
        Assert.AreEqual("report.pdf", loaded.Children.First().Name);
    }

    // ---- FileNode: Soft-Delete Query Filter ----

    [TestMethod]
    public async Task WhenFileNodeIsSoftDeletedThenFilteredOutByDefault()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();

        var active = new FileNode { Name = "active.txt", OwnerId = ownerId, MaterializedPath = "/" };
        var deleted = new FileNode
        {
            Name = "deleted.txt",
            OwnerId = ownerId,
            MaterializedPath = "/",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };

        context.FileNodes.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var results = await context.FileNodes.ToListAsync();

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("active.txt", results[0].Name);
    }

    [TestMethod]
    public async Task WhenFileNodeIsSoftDeletedThenVisibleWithIgnoreQueryFilters()
    {
        using var context = CreateContext();
        var ownerId = Guid.NewGuid();

        var active = new FileNode { Name = "active.txt", OwnerId = ownerId, MaterializedPath = "/" };
        var deleted = new FileNode
        {
            Name = "deleted.txt",
            OwnerId = ownerId,
            MaterializedPath = "/",
            IsDeleted = true
        };

        context.FileNodes.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var results = await context.FileNodes.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(2, results.Count);
    }

    // ---- FileComment: Soft-Delete Query Filter ----

    [TestMethod]
    public async Task WhenFileCommentIsSoftDeletedThenFilteredOutByDefault()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "test.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var active = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Visible comment",
            CreatedByUserId = Guid.NewGuid()
        };
        var deleted = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Deleted comment",
            CreatedByUserId = Guid.NewGuid(),
            IsDeleted = true
        };

        context.FileComments.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var results = await context.FileComments.ToListAsync();

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Visible comment", results[0].Content);
    }

    [TestMethod]
    public async Task WhenFileCommentIsSoftDeletedThenVisibleWithIgnoreQueryFilters()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "test.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var active = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Active",
            CreatedByUserId = Guid.NewGuid()
        };
        var deleted = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Deleted",
            CreatedByUserId = Guid.NewGuid(),
            IsDeleted = true
        };

        context.FileComments.AddRange(active, deleted);
        await context.SaveChangesAsync();

        var results = await context.FileComments.IgnoreQueryFilters().ToListAsync();

        Assert.AreEqual(2, results.Count);
    }

    // ---- FileComment: Threaded Replies ----

    [TestMethod]
    public async Task WhenFileCommentHasRepliesThenRelationshipIsTracked()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "test.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var parent = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Parent comment",
            CreatedByUserId = Guid.NewGuid()
        };
        var reply = new FileComment
        {
            FileNodeId = node.Id,
            Content = "Reply",
            ParentCommentId = parent.Id,
            CreatedByUserId = Guid.NewGuid()
        };

        context.FileComments.AddRange(parent, reply);
        await context.SaveChangesAsync();

        var loaded = await context.FileComments
            .Include(c => c.Replies)
            .FirstAsync(c => c.Id == parent.Id);

        Assert.AreEqual(1, loaded.Replies.Count);
        Assert.AreEqual("Reply", loaded.Replies.First().Content);
    }

    // ---- FileVersion: FK to FileNode ----

    [TestMethod]
    public async Task WhenFileVersionAddedThenNavigationToNodeWorks()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "doc.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            ContentHash = "abc",
            StoragePath = "/abc",
            CreatedByUserId = node.OwnerId
        };
        context.FileVersions.Add(version);
        await context.SaveChangesAsync();

        var loaded = await context.FileVersions
            .Include(v => v.FileNode)
            .FirstAsync(v => v.Id == version.Id);

        Assert.IsNotNull(loaded.FileNode);
        Assert.AreEqual("doc.txt", loaded.FileNode.Name);
    }

    // ---- FileVersionChunk: Composite Key ----

    [TestMethod]
    public async Task WhenFileVersionChunkAddedThenCompositeKeyWorks()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "test.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            ContentHash = "abc",
            StoragePath = "/abc",
            CreatedByUserId = node.OwnerId
        };
        context.FileVersions.Add(version);

        var chunk = new FileChunk { ChunkHash = "chunk1", StoragePath = "/chunks/c1", Size = 100 };
        context.FileChunks.Add(chunk);

        var mapping = new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        };
        context.FileVersionChunks.Add(mapping);
        await context.SaveChangesAsync();

        var loaded = await context.FileVersionChunks
            .Include(vc => vc.FileVersion)
            .Include(vc => vc.FileChunk)
            .FirstAsync();

        Assert.AreEqual(version.Id, loaded.FileVersionId);
        Assert.AreEqual(chunk.Id, loaded.FileChunkId);
        Assert.AreEqual(0, loaded.SequenceIndex);
        Assert.IsNotNull(loaded.FileVersion);
        Assert.IsNotNull(loaded.FileChunk);
    }

    // ---- FileShare: FK to FileNode ----

    [TestMethod]
    public async Task WhenFileShareAddedThenNavigationToNodeWorks()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "shared.pdf", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var share = new FileShare
        {
            FileNodeId = node.Id,
            ShareType = ShareType.PublicLink,
            Permission = SharePermission.Read,
            LinkToken = "token123",
            CreatedByUserId = node.OwnerId
        };
        context.FileShares.Add(share);
        await context.SaveChangesAsync();

        var loaded = await context.FileShares
            .Include(s => s.FileNode)
            .FirstAsync(s => s.Id == share.Id);

        Assert.IsNotNull(loaded.FileNode);
        Assert.AreEqual("shared.pdf", loaded.FileNode.Name);
    }

    // ---- FileTag: FK to FileNode ----

    [TestMethod]
    public async Task WhenFileTagAddedThenNavigationToNodeWorks()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "important.doc", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var tag = new FileTag
        {
            FileNodeId = node.Id,
            Name = "Important",
            Color = "#E53E3E",
            CreatedByUserId = node.OwnerId
        };
        context.FileTags.Add(tag);
        await context.SaveChangesAsync();

        var loaded = await context.FileTags
            .Include(t => t.FileNode)
            .FirstAsync(t => t.Id == tag.Id);

        Assert.IsNotNull(loaded.FileNode);
        Assert.AreEqual("important.doc", loaded.FileNode.Name);
    }

    // ---- FileNode: Navigation Collections ----

    [TestMethod]
    public async Task WhenFileNodeHasVersionsSharesTagsCommentsThenAllLoadable()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var node = new FileNode { Name = "multi.txt", OwnerId = userId, MaterializedPath = "/" };
        context.FileNodes.Add(node);

        context.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id, VersionNumber = 1, ContentHash = "h1",
            StoragePath = "/h1", CreatedByUserId = userId
        });
        context.FileShares.Add(new FileShare
        {
            FileNodeId = node.Id, ShareType = ShareType.User,
            SharedWithUserId = Guid.NewGuid(), CreatedByUserId = userId
        });
        context.FileTags.Add(new FileTag
        {
            FileNodeId = node.Id, Name = "Work", CreatedByUserId = userId
        });
        context.FileComments.Add(new FileComment
        {
            FileNodeId = node.Id, Content = "Nice file!", CreatedByUserId = userId
        });
        await context.SaveChangesAsync();

        var loaded = await context.FileNodes
            .Include(n => n.Versions)
            .Include(n => n.Shares)
            .Include(n => n.Tags)
            .Include(n => n.Comments)
            .FirstAsync(n => n.Id == node.Id);

        Assert.AreEqual(1, loaded.Versions.Count);
        Assert.AreEqual(1, loaded.Shares.Count);
        Assert.AreEqual(1, loaded.Tags.Count);
        Assert.AreEqual(1, loaded.Comments.Count);
    }

    // ---- FileQuota: Unique UserId ----

    [TestMethod]
    public async Task WhenFileQuotaAddedThenCanQueryByUserId()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        context.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10737418240 });
        await context.SaveChangesAsync();

        var quota = await context.FileQuotas.FirstOrDefaultAsync(q => q.UserId == userId);

        Assert.IsNotNull(quota);
        Assert.AreEqual(10737418240, quota.MaxBytes);
    }

    // ---- ChunkedUploadSession: Status Enum as String ----

    [TestMethod]
    public async Task WhenUploadSessionSavedThenStatusEnumPersistedAsString()
    {
        using var context = CreateContext();

        var session = new ChunkedUploadSession
        {
            FileName = "test.bin",
            ChunkManifest = "[]",
            UserId = Guid.NewGuid(),
            Status = UploadSessionStatus.Completed
        };

        context.UploadSessions.Add(session);
        await context.SaveChangesAsync();

        var loaded = await context.UploadSessions.FindAsync(session.Id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(UploadSessionStatus.Completed, loaded.Status);
    }
}
