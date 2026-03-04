using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class VersionServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static VersionService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<VersionService>());

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private static (FileNode Node, FileVersion Version, FileChunk Chunk) SeedFileWithVersion(FilesDbContext db, Guid userId)
    {
        var node = new FileNode
        {
            Name = "file.txt",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            CurrentVersion = 1,
            ContentHash = "hash_v1",
            StoragePath = "files/v1"
        };
        db.FileNodes.Add(node);

        var chunk = new FileChunk { ChunkHash = "chunk_hash", StoragePath = "chunks/ch/un/chunk_hash", Size = 100, ReferenceCount = 1 };
        db.FileChunks.Add(chunk);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 100,
            ContentHash = "hash_v1",
            StoragePath = "files/v1",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);

        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });

        return (node, version, chunk);
    }

    [TestMethod]
    public async Task ListVersionsAsync_ReturnsVersionsDescending()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "file.txt", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        db.FileVersions.Add(new FileVersion { FileNodeId = node.Id, VersionNumber = 1, Size = 100, ContentHash = "h1", StoragePath = "p1", CreatedByUserId = userId });
        db.FileVersions.Add(new FileVersion { FileNodeId = node.Id, VersionNumber = 2, Size = 200, ContentHash = "h2", StoragePath = "p2", CreatedByUserId = userId });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var versions = await service.ListVersionsAsync(node.Id, UserCaller(userId));

        Assert.AreEqual(2, versions.Count);
        Assert.AreEqual(2, versions[0].VersionNumber);
        Assert.AreEqual(1, versions[1].VersionNumber);
    }

    [TestMethod]
    public async Task GetVersionAsync_ExistingVersion_ReturnsDto()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var version = new FileVersion
        {
            FileNodeId = Guid.NewGuid(),
            VersionNumber = 1,
            Size = 100,
            ContentHash = "hash",
            StoragePath = "path",
            CreatedByUserId = userId,
            Label = "Draft"
        };
        db.FileVersions.Add(version);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetVersionAsync(version.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.AreEqual("Draft", result.Label);
    }

    [TestMethod]
    public async Task GetVersionAsync_NonExistent_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.GetVersionAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid()));
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RestoreVersionAsync_CreatesNewVersionFromOld()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var (node, version, chunk) = SeedFileWithVersion(db, userId);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.RestoreVersionAsync(node.Id, version.Id, UserCaller(userId));

        Assert.AreEqual(2, result.VersionNumber);
        Assert.AreEqual(version.ContentHash, result.ContentHash);
        Assert.IsTrue(result.Label!.Contains("Restored"));

        // Chunk refcount should have incremented
        var updatedChunk = await db.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(2, updatedChunk!.ReferenceCount);
    }

    [TestMethod]
    public async Task RestoreVersionAsync_Folder_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Folder", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.RestoreVersionAsync(folder.Id, Guid.NewGuid(), UserCaller(userId)));
    }

    [TestMethod]
    public async Task LabelVersionAsync_UpdatesLabel()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var version = new FileVersion
        {
            FileNodeId = Guid.NewGuid(),
            VersionNumber = 1,
            Size = 100,
            ContentHash = "hash",
            StoragePath = "path",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(version);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.LabelVersionAsync(version.Id, "Final", UserCaller(userId));

        Assert.AreEqual("Final", result.Label);
    }

    [TestMethod]
    public async Task DeleteVersionAsync_WithMultipleVersions_DeletesAndDecrementsRefcount()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var (node, v1, chunk) = SeedFileWithVersion(db, userId);

        // Add a second version
        var v2 = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 2,
            Size = 200,
            ContentHash = "hash_v2",
            StoragePath = "files/v2",
            CreatedByUserId = userId
        };
        db.FileVersions.Add(v2);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.DeleteVersionAsync(v1.Id, UserCaller(userId));

        // Version should be deleted
        var deleted = await db.FileVersions.FindAsync(v1.Id);
        Assert.IsNull(deleted);

        // Chunk refcount should have decremented
        var updatedChunk = await db.FileChunks.FindAsync(chunk.Id);
        Assert.AreEqual(0, updatedChunk!.ReferenceCount);
    }

    [TestMethod]
    public async Task DeleteVersionAsync_OnlyVersion_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var (_, version, _) = SeedFileWithVersion(db, userId);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.DeleteVersionAsync(version.Id, UserCaller(userId)));
    }
}
