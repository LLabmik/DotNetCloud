using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Data;

/// <summary>
/// Tests for <see cref="FilesDbInitializer"/> verifying root folder creation,
/// quota seeding, default tags, and idempotency.
/// </summary>
[TestClass]
public class FilesDbInitializerTests
{
    private static FilesDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    // ---- EnsureRootFolderAsync ----

    [TestMethod]
    public async Task WhenEnsureRootFolderCalledThenCreatesRootFolder()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var root = await FilesDbInitializer.EnsureRootFolderAsync(context, userId);

        Assert.IsNotNull(root);
        Assert.AreEqual("Root", root.Name);
        Assert.AreEqual(FileNodeType.Folder, root.NodeType);
        Assert.AreEqual(userId, root.OwnerId);
        Assert.IsNull(root.ParentId);
        Assert.AreEqual(0, root.Depth);
        Assert.IsTrue(root.MaterializedPath.StartsWith("/"));
    }

    [TestMethod]
    public async Task WhenEnsureRootFolderCalledTwiceThenReturnsSameFolder()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var root1 = await FilesDbInitializer.EnsureRootFolderAsync(context, userId);
        var root2 = await FilesDbInitializer.EnsureRootFolderAsync(context, userId);

        Assert.AreEqual(root1.Id, root2.Id);
    }

    [TestMethod]
    public async Task WhenEnsureRootFolderCalledForDifferentUsersThenCreatesSeparateRoots()
    {
        using var context = CreateContext();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var root1 = await FilesDbInitializer.EnsureRootFolderAsync(context, user1);
        var root2 = await FilesDbInitializer.EnsureRootFolderAsync(context, user2);

        Assert.AreNotEqual(root1.Id, root2.Id);
        Assert.AreEqual(user1, root1.OwnerId);
        Assert.AreEqual(user2, root2.OwnerId);
    }

    [TestMethod]
    public async Task WhenEnsureRootFolderCalledThenRootPersistedInDatabase()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        await FilesDbInitializer.EnsureRootFolderAsync(context, userId);

        var count = await context.FileNodes.IgnoreQueryFilters().CountAsync();
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task WhenNullDbPassedToEnsureRootFolderThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => FilesDbInitializer.EnsureRootFolderAsync(null!, Guid.NewGuid()));
    }

    // ---- EnsureQuotaAsync ----

    [TestMethod]
    public async Task WhenEnsureQuotaCalledThenCreatesQuota()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var quota = await FilesDbInitializer.EnsureQuotaAsync(context, userId);

        Assert.IsNotNull(quota);
        Assert.AreEqual(userId, quota.UserId);
        Assert.AreEqual(FilesDbInitializer.DefaultQuotaBytes, quota.MaxBytes);
        Assert.AreEqual(0, quota.UsedBytes);
    }

    [TestMethod]
    public async Task WhenEnsureQuotaCalledWithCustomSizeThenUsesCustomSize()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        long customQuota = 5L * 1024 * 1024 * 1024; // 5 GB

        var quota = await FilesDbInitializer.EnsureQuotaAsync(context, userId, customQuota);

        Assert.AreEqual(customQuota, quota.MaxBytes);
    }

    [TestMethod]
    public async Task WhenEnsureQuotaCalledTwiceThenReturnsSameQuota()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var quota1 = await FilesDbInitializer.EnsureQuotaAsync(context, userId);
        var quota2 = await FilesDbInitializer.EnsureQuotaAsync(context, userId);

        Assert.AreEqual(quota1.Id, quota2.Id);
    }

    [TestMethod]
    public async Task WhenEnsureQuotaCalledThenPersistedInDatabase()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        await FilesDbInitializer.EnsureQuotaAsync(context, userId);

        var count = await context.FileQuotas.CountAsync();
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task WhenDefaultQuotaBytesCheckedThenIs10GB()
    {
#pragma warning disable MSTEST0032 // Canary test guards compile-time constant value
        Assert.AreEqual(10L * 1024 * 1024 * 1024, FilesDbInitializer.DefaultQuotaBytes);
#pragma warning restore MSTEST0032
        await Task.CompletedTask;
    }

    [TestMethod]
    public async Task WhenNullDbPassedToEnsureQuotaThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => FilesDbInitializer.EnsureQuotaAsync(null!, Guid.NewGuid()));
    }

    // ---- SeedDefaultTagsAsync ----

    [TestMethod]
    public async Task WhenSeedDefaultTagsCalledThenCreatesThreeTags()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        // Create a root node first so FK is valid
        context.FileNodes.Add(new FileNode
        {
            Id = rootId,
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/"
        });
        await context.SaveChangesAsync();

        await FilesDbInitializer.SeedDefaultTagsAsync(context, userId, rootId);

        var tags = await context.FileTags.ToListAsync();
        Assert.AreEqual(3, tags.Count);
    }

    [TestMethod]
    public async Task WhenSeedDefaultTagsCalledThenCreatesExpectedTagNames()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        context.FileNodes.Add(new FileNode
        {
            Id = rootId,
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/"
        });
        await context.SaveChangesAsync();

        await FilesDbInitializer.SeedDefaultTagsAsync(context, userId, rootId);

        var tagNames = await context.FileTags.Select(t => t.Name).ToListAsync();
        CollectionAssert.Contains(tagNames, "Important");
        CollectionAssert.Contains(tagNames, "Work");
        CollectionAssert.Contains(tagNames, "Personal");
    }

    [TestMethod]
    public async Task WhenSeedDefaultTagsCalledThenTagsHaveColors()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        context.FileNodes.Add(new FileNode
        {
            Id = rootId,
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/"
        });
        await context.SaveChangesAsync();

        await FilesDbInitializer.SeedDefaultTagsAsync(context, userId, rootId);

        var tags = await context.FileTags.ToListAsync();
        Assert.IsTrue(tags.All(t => !string.IsNullOrEmpty(t.Color)));
        Assert.IsTrue(tags.All(t => t.Color!.StartsWith("#")));
    }

    [TestMethod]
    public async Task WhenSeedDefaultTagsCalledTwiceThenDoesNotDuplicate()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        context.FileNodes.Add(new FileNode
        {
            Id = rootId,
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = "/"
        });
        await context.SaveChangesAsync();

        await FilesDbInitializer.SeedDefaultTagsAsync(context, userId, rootId);
        await FilesDbInitializer.SeedDefaultTagsAsync(context, userId, rootId);

        var count = await context.FileTags.CountAsync();
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task WhenNullDbPassedToSeedDefaultTagsThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => FilesDbInitializer.SeedDefaultTagsAsync(null!, Guid.NewGuid(), Guid.NewGuid()));
    }

    // ---- InitializeUserAsync ----

    [TestMethod]
    public async Task WhenInitializeUserCalledThenCreatesRootFolderQuotaAndTags()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var logger = NullLogger.Instance;

        await FilesDbInitializer.InitializeUserAsync(context, userId, logger: logger);

        var nodes = await context.FileNodes.IgnoreQueryFilters().CountAsync();
        var quotas = await context.FileQuotas.CountAsync();
        var tags = await context.FileTags.CountAsync();

        Assert.AreEqual(1, nodes);
        Assert.AreEqual(1, quotas);
        Assert.AreEqual(3, tags);
    }

    [TestMethod]
    public async Task WhenInitializeUserCalledTwiceThenIsIdempotent()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        await FilesDbInitializer.InitializeUserAsync(context, userId);
        await FilesDbInitializer.InitializeUserAsync(context, userId);

        var nodes = await context.FileNodes.IgnoreQueryFilters().CountAsync();
        var quotas = await context.FileQuotas.CountAsync();
        var tags = await context.FileTags.CountAsync();

        Assert.AreEqual(1, nodes);
        Assert.AreEqual(1, quotas);
        Assert.AreEqual(3, tags);
    }

    [TestMethod]
    public async Task WhenInitializeUserCalledForTwoUsersThenSeparateData()
    {
        using var context = CreateContext();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await FilesDbInitializer.InitializeUserAsync(context, user1);
        await FilesDbInitializer.InitializeUserAsync(context, user2);

        var nodes = await context.FileNodes.IgnoreQueryFilters().CountAsync();
        var quotas = await context.FileQuotas.CountAsync();
        var tags = await context.FileTags.CountAsync();

        Assert.AreEqual(2, nodes);
        Assert.AreEqual(2, quotas);
        Assert.AreEqual(6, tags);
    }

    [TestMethod]
    public async Task WhenNullDbPassedToInitializeUserThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => FilesDbInitializer.InitializeUserAsync(null!, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task WhenInitializeUserCalledWithCustomQuotaThenUsesCustomQuota()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        long customQuota = 1L * 1024 * 1024 * 1024; // 1 GB

        await FilesDbInitializer.InitializeUserAsync(context, userId, customQuota);

        var quota = await context.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(customQuota, quota.MaxBytes);
    }
}
