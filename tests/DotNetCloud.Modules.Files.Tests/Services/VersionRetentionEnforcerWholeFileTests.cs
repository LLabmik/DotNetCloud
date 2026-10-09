using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="VersionRetentionEnforcer"/> surfacing pruned whole-file blob paths.
/// </summary>
[TestClass]
public class VersionRetentionEnforcerWholeFileTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    [TestMethod]
    public async Task ApplyAsync_PruningWholeFileVersion_ReturnsItsBlobPath()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var node = new FileNode { Name = "clip.mp4", NodeType = FileNodeType.File, OwnerId = owner, MimeType = "video/mp4" };
        db.FileNodes.Add(node);

        const string wholeFileBlob = "files/aa/bb/oldversionblob";
        var v1 = NewVersion(node.Id, 1, owner, isChunked: false, storagePath: wholeFileBlob);
        var v2 = NewVersion(node.Id, 2, owner, isChunked: true, storagePath: "files/cc/dd/v2");
        var v3 = NewVersion(node.Id, 3, owner, isChunked: true, storagePath: "files/ee/ff/v3");
        db.FileVersions.AddRange(v1, v2, v3);
        await db.SaveChangesAsync();

        var result = await VersionRetentionEnforcer.ApplyAsync(
            db, node.Id, new VersionRetentionOptions { MaxVersionCount = 2 });

        Assert.AreEqual(1, result.PrunedCount);
        CollectionAssert.Contains(result.PrunedWholeFilePaths.ToList(), wholeFileBlob);
    }

    [TestMethod]
    public async Task ApplyAsync_PruningChunkedVersion_ReturnsNoBlobPaths()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var node = new FileNode { Name = "report.pdf", NodeType = FileNodeType.File, OwnerId = owner, MimeType = "application/pdf" };
        db.FileNodes.Add(node);

        db.FileVersions.AddRange(
            NewVersion(node.Id, 1, owner, isChunked: true, storagePath: "files/aa/bb/v1"),
            NewVersion(node.Id, 2, owner, isChunked: true, storagePath: "files/cc/dd/v2"),
            NewVersion(node.Id, 3, owner, isChunked: true, storagePath: "files/ee/ff/v3"));
        await db.SaveChangesAsync();

        var result = await VersionRetentionEnforcer.ApplyAsync(
            db, node.Id, new VersionRetentionOptions { MaxVersionCount = 2 });

        Assert.AreEqual(1, result.PrunedCount);
        Assert.AreEqual(0, result.PrunedWholeFilePaths.Count);
    }

    [TestMethod]
    public async Task ApplyAsync_UnlimitedHistory_ReturnsNone()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var node = new FileNode { Name = "clip.mp4", NodeType = FileNodeType.File, OwnerId = owner };
        db.FileNodes.Add(node);
        db.FileVersions.Add(NewVersion(node.Id, 1, owner, isChunked: false, storagePath: "files/aa/bb/only"));
        await db.SaveChangesAsync();

        var result = await VersionRetentionEnforcer.ApplyAsync(
            db, node.Id, new VersionRetentionOptions { MaxVersionCount = 0, RetentionDays = 0 });

        Assert.AreEqual(0, result.PrunedCount);
        Assert.AreEqual(0, result.PrunedWholeFilePaths.Count);
    }

    private static FileVersion NewVersion(Guid nodeId, int number, Guid owner, bool isChunked, string storagePath) =>
        new()
        {
            FileNodeId = nodeId,
            VersionNumber = number,
            Size = 10,
            ContentHash = $"hash{number}",
            StoragePath = storagePath,
            CreatedByUserId = owner,
            IsChunked = isChunked,
            CreatedAt = DateTime.UtcNow.AddMinutes(number)
        };
}
