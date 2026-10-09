using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="WholeFileBlobIntegrityService"/>: it must find whole-file versions whose
/// content has gone missing from storage, and separate recoverable losses (chunks survive) from
/// unrecoverable ones.
/// </summary>
[TestClass]
public class WholeFileBlobIntegrityServiceTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-integrity-{Guid.CreateVersion7():N}");
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

    private IWholeFileBlobIntegrityService CreateService(FilesDbContext db) =>
        new WholeFileBlobIntegrityService(db, _engine);

    private static async Task<FileVersion> AddVersionAsync(
        FilesDbContext db,
        string name,
        long size,
        string storagePath,
        bool isChunked = false,
        bool nodeDeleted = false,
        int chunkMappings = 0)
    {
        var owner = Guid.CreateVersion7();
        var node = new FileNode
        {
            Name = name,
            NodeType = FileNodeType.File,
            OwnerId = owner,
            MimeType = "video/mp4",
            Size = size,
            IsDeleted = nodeDeleted,
            DeletedAt = nodeDeleted ? DateTime.UtcNow : null
        };
        db.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = size,
            ContentHash = $"{name}-hash",
            StoragePath = storagePath,
            MimeType = "video/mp4",
            CreatedByUserId = owner,
            IsChunked = isChunked
        };
        db.FileVersions.Add(version);

        for (var i = 0; i < chunkMappings; i++)
        {
            var chunk = new FileChunk
            {
                ChunkHash = $"{name}-chunk-{i}",
                Size = 1,
                StoragePath = $"chunks/aa/bb/{name}-chunk-{i}",
                ReferenceCount = 1
            };
            db.FileChunks.Add(chunk);
            db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = version.Id,
                FileChunkId = chunk.Id,
                SequenceIndex = i
            });
        }

        await db.SaveChangesAsync();
        return version;
    }

    [TestMethod]
    public async Task AuditAsync_BlobPresent_IsHealthy()
    {
        using var db = CreateContext();
        const string storagePath = "files/ab/cd/intact";
        await _engine.WriteChunkAsync(storagePath, "complete-content"u8.ToArray());
        await AddVersionAsync(db, "intact.mp4", 16, storagePath);

        var report = await CreateService(db).AuditAsync();

        Assert.IsTrue(report.IsHealthy);
        Assert.AreEqual(1, report.ScannedVersions);
        Assert.AreEqual(0, report.Defects.Count);
    }

    [TestMethod]
    public async Task AuditAsync_BlobMissingWithoutChunks_IsReportedUnrecoverable()
    {
        using var db = CreateContext();
        await AddVersionAsync(db, "lost.mp4", 1024, "files/ab/cd/lost");

        var report = await CreateService(db).AuditAsync();

        Assert.IsFalse(report.IsHealthy);
        var defect = report.Defects.Single();
        Assert.AreEqual("lost.mp4", defect.FileName);
        Assert.AreEqual(1, report.UnrecoverableCount);
        Assert.AreEqual(0, report.RecoverableCount);
        Assert.IsFalse(defect.IsRecoverable, "No chunk mappings survive, so the content cannot be rebuilt.");
        Assert.AreEqual("files/ab/cd/lost", defect.StoragePath);
        Assert.AreEqual(1024, defect.Size);
    }

    [TestMethod]
    public async Task AuditAsync_BlobMissingButChunksRemain_IsReportedRecoverable()
    {
        using var db = CreateContext();
        await AddVersionAsync(db, "rebuildable.mp4", 2048, "files/ab/cd/rebuildable", chunkMappings: 3);

        var report = await CreateService(db).AuditAsync();

        var defect = report.Defects.Single();
        Assert.IsTrue(defect.IsRecoverable);
        Assert.AreEqual(3, defect.ChunkMappingsRemaining);
        Assert.AreEqual(1, report.RecoverableCount);
        Assert.AreEqual(0, report.UnrecoverableCount);
    }

    [TestMethod]
    public async Task AuditAsync_TruncatedBlob_IsReportedAsDefect()
    {
        using var db = CreateContext();
        const string storagePath = "files/ab/cd/truncated";
        await _engine.WriteChunkAsync(storagePath, "short"u8.ToArray());
        await AddVersionAsync(db, "truncated.mp4", 4096, storagePath);

        var report = await CreateService(db).AuditAsync();

        Assert.IsFalse(report.IsHealthy, "A blob shorter than the recorded size is a defect.");
        Assert.AreEqual("truncated.mp4", report.Defects.Single().FileName);
    }

    [TestMethod]
    public async Task AuditAsync_TrashedNode_IsIgnored()
    {
        using var db = CreateContext();
        await AddVersionAsync(db, "trashed.mp4", 512, "files/ab/cd/trashed", nodeDeleted: true);

        var report = await CreateService(db).AuditAsync();

        Assert.IsTrue(report.IsHealthy, "Content in the trash is expected to still be on disk; it is not an alert.");
    }

    [TestMethod]
    public async Task AuditAsync_ChunkedVersion_IsNotScanned()
    {
        using var db = CreateContext();
        await AddVersionAsync(db, "chunked.mp4", 512, "files/ab/cd/chunked", isChunked: true);

        var report = await CreateService(db).AuditAsync();

        Assert.IsTrue(report.IsHealthy);
        Assert.AreEqual(0, report.ScannedVersions, "Only whole-file versions are audited.");
    }
}
