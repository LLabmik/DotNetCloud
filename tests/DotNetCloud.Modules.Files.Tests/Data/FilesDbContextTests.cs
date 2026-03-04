using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

using FileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests for <see cref="FilesDbContext"/> verifying initialization, DbSets, and model configuration.
/// </summary>
[TestClass]
public class FilesDbContextTests
{
    private static FilesDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    // ---- Initialization ----

    [TestMethod]
    public void WhenCreatedThenInitializesSuccessfully()
    {
        using var context = CreateContext();

        Assert.IsNotNull(context);
        Assert.IsNotNull(context.Model);
    }

    // ---- DbSets ----

    [TestMethod]
    public void WhenCreatedThenHasFileNodesDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileNodes);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileVersionsDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileVersions);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileChunksDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileChunks);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileVersionChunksDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileVersionChunks);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileSharesDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileShares);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileTagsDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileTags);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileCommentsDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileComments);
    }

    [TestMethod]
    public void WhenCreatedThenHasFileQuotasDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.FileQuotas);
    }

    [TestMethod]
    public void WhenCreatedThenHasUploadSessionsDbSet()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context.UploadSessions);
    }

    [TestMethod]
    public void WhenCreatedThenHasNineDbSets()
    {
        using var context = CreateContext();

        // Verify all 9 entity types are in the model
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

        Assert.IsTrue(entityTypes.Contains(typeof(FileNode)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileVersion)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileChunk)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileVersionChunk)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileShare)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileTag)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileComment)));
        Assert.IsTrue(entityTypes.Contains(typeof(FileQuota)));
        Assert.IsTrue(entityTypes.Contains(typeof(ChunkedUploadSession)));
    }

    // ---- CRUD Operations ----

    [TestMethod]
    public async Task WhenFileNodeAddedThenCanBeRetrieved()
    {
        using var context = CreateContext();

        var node = new FileNode
        {
            Name = "test.txt",
            OwnerId = Guid.NewGuid(),
            MaterializedPath = "/root"
        };

        context.FileNodes.Add(node);
        await context.SaveChangesAsync();

        var retrieved = await context.FileNodes.FindAsync(node.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("test.txt", retrieved.Name);
    }

    [TestMethod]
    public async Task WhenFileVersionAddedThenCanBeRetrieved()
    {
        using var context = CreateContext();

        var node = new FileNode { Name = "test.txt", OwnerId = Guid.NewGuid(), MaterializedPath = "/" };
        context.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            ContentHash = "abc123",
            StoragePath = "/files/abc123",
            Size = 100,
            CreatedByUserId = node.OwnerId
        };
        context.FileVersions.Add(version);
        await context.SaveChangesAsync();

        var retrieved = await context.FileVersions.FindAsync(version.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(1, retrieved.VersionNumber);
    }

    [TestMethod]
    public async Task WhenFileQuotaAddedThenCanBeRetrieved()
    {
        using var context = CreateContext();

        var quota = new FileQuota
        {
            UserId = Guid.NewGuid(),
            MaxBytes = 10737418240
        };

        context.FileQuotas.Add(quota);
        await context.SaveChangesAsync();

        var retrieved = await context.FileQuotas.FindAsync(quota.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(10737418240, retrieved.MaxBytes);
    }

    [TestMethod]
    public async Task WhenUploadSessionAddedThenCanBeRetrieved()
    {
        using var context = CreateContext();

        var session = new ChunkedUploadSession
        {
            FileName = "upload.bin",
            ChunkManifest = "[\"hash1\"]",
            UserId = Guid.NewGuid(),
            TotalSize = 4194304,
            TotalChunks = 1
        };

        context.UploadSessions.Add(session);
        await context.SaveChangesAsync();

        var retrieved = await context.UploadSessions.FindAsync(session.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("upload.bin", retrieved.FileName);
    }
}
