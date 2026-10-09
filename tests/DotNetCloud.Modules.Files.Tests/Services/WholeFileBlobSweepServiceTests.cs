using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="WholeFileBlobSweepService"/> disk/DB reconciliation.
/// </summary>
[TestClass]
public class WholeFileBlobSweepServiceTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-blobsweep-{Guid.CreateVersion7():N}");
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

    private WholeFileBlobSweepService CreateService(FilesDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IFileStorageEngine>(_engine);
        var provider = services.BuildServiceProvider();

        return new WholeFileBlobSweepService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WholeFileBlobSweepService>.Instance,
            Mock.Of<IBackgroundServiceTracker>());
    }

    [TestMethod]
    public async Task SweepOnceAsync_UnreferencedBlob_IsDeleted()
    {
        using var db = CreateContext();
        const string blob = "files/ab/cd/orphanblob";
        await _engine.WriteChunkAsync(blob, "orphan"u8.ToArray());

        await CreateService(db).SweepOnceAsync(CancellationToken.None);

        Assert.IsFalse(await _engine.ExistsAsync(blob));
    }

    [TestMethod]
    public async Task SweepOnceAsync_ReferencedByVersion_IsKept()
    {
        using var db = CreateContext();
        const string blob = "files/ab/cd/referencedblob";
        await _engine.WriteChunkAsync(blob, "keep"u8.ToArray());

        var node = new FileNode { Name = "movie.mp4", NodeType = FileNodeType.File, OwnerId = Guid.CreateVersion7() };
        db.FileNodes.Add(node);
        db.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 4,
            ContentHash = "referencedblob",
            StoragePath = blob,
            IsChunked = false
        });
        await db.SaveChangesAsync();

        await CreateService(db).SweepOnceAsync(CancellationToken.None);

        Assert.IsTrue(await _engine.ExistsAsync(blob));
    }

    [TestMethod]
    public async Task SweepOnceAsync_ScratchFile_IsDeleted()
    {
        using var db = CreateContext();
        const string scratch = "files/ab/cd/blob.tmp-0123456789abcdef";
        await _engine.WriteChunkAsync(scratch, "partial"u8.ToArray());

        await CreateService(db).SweepOnceAsync(CancellationToken.None);

        Assert.IsFalse(await _engine.ExistsAsync(scratch));
    }

    [TestMethod]
    public async Task SweepOnceAsync_ChunkBlobs_AreUntouched()
    {
        using var db = CreateContext();
        const string chunk = "chunks/ab/cd/somechunk";
        await _engine.WriteChunkAsync(chunk, "chunk"u8.ToArray());

        await CreateService(db).SweepOnceAsync(CancellationToken.None);

        Assert.IsTrue(await _engine.ExistsAsync(chunk), "The sweep only manages the files/ whole-file pool.");
    }
}
