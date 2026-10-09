using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="WholeFileBlobCleanup"/> unreferenced-blob deletion.
/// </summary>
[TestClass]
public class WholeFileBlobCleanupTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-blobcleanup-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_basePath);
        _engine = new LocalFileStorageEngine(_basePath, NullLogger<LocalFileStorageEngine>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private const string BlobPath = "files/ab/cd/abcdef0123456789";

    [TestMethod]
    public async Task DeleteIfUnreferencedAsync_UnreferencedBlob_Deletes()
    {
        using var db = CreateContext();
        await _engine.WriteChunkAsync(BlobPath, "payload"u8.ToArray());

        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, BlobPath);

        Assert.IsFalse(await _engine.ExistsAsync(BlobPath));
    }

    [TestMethod]
    public async Task DeleteIfUnreferencedAsync_ReferencedByVersion_Keeps()
    {
        using var db = CreateContext();
        await _engine.WriteChunkAsync(BlobPath, "payload"u8.ToArray());
        db.FileVersions.Add(new FileVersion
        {
            FileNodeId = Guid.CreateVersion7(),
            VersionNumber = 1,
            Size = 7,
            ContentHash = "abcdef0123456789",
            StoragePath = BlobPath,
            IsChunked = false
        });
        await db.SaveChangesAsync();

        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, BlobPath);

        Assert.IsTrue(await _engine.ExistsAsync(BlobPath));
    }

    [TestMethod]
    public async Task DeleteIfUnreferencedAsync_ReferencedByTrashedNode_Keeps()
    {
        using var db = CreateContext();
        await _engine.WriteChunkAsync(BlobPath, "payload"u8.ToArray());
        db.FileNodes.Add(new FileNode
        {
            Name = "clip.mp4",
            NodeType = FileNodeType.File,
            OwnerId = Guid.CreateVersion7(),
            StoragePath = BlobPath,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedByUserId = Guid.CreateVersion7()
        });
        await db.SaveChangesAsync();

        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, BlobPath);

        Assert.IsTrue(await _engine.ExistsAsync(BlobPath), "A trashed node still needs its blob for restore.");
    }

    [TestMethod]
    public async Task DeleteIfUnreferencedAsync_ChunkPath_IsNoOp()
    {
        using var db = CreateContext();
        const string chunkPath = "chunks/ab/cd/abcdef";
        await _engine.WriteChunkAsync(chunkPath, "payload"u8.ToArray());

        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, chunkPath);

        Assert.IsTrue(await _engine.ExistsAsync(chunkPath), "Chunk paths are never handled by the whole-file cleanup.");
    }

    [TestMethod]
    public async Task DeleteIfUnreferencedAsync_NullOrEmptyPath_IsNoOp()
    {
        using var db = CreateContext();

        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, null);
        await WholeFileBlobCleanup.DeleteIfUnreferencedAsync(db, _engine, "");
    }
}
