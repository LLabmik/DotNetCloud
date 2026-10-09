using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Upload-completion tests confirming immutable media converts to whole-file storage while
/// documents keep the chunk pipeline.
/// </summary>
[TestClass]
public class ChunkedUploadServiceWholeFileTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-upload-wholefile-{Guid.CreateVersion7():N}");
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

    private ChunkedUploadService CreateService(FilesDbContext db)
    {
        var wholeFile = new WholeFileStorageService(db, _engine,
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()), NullLogger<WholeFileStorageService>.Instance);

        var quota = new Mock<IQuotaService>();
        quota.Setup(q => q.HasSufficientQuotaAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        quota.Setup(q => q.TryReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new ChunkedUploadService(
            db,
            _engine,
            quota.Object,
            Mock.Of<IEventBus>(),
            new DeviceContext(),
            Mock.Of<ISyncChangeNotifier>(),
            NullLoggerFactory.Instance.CreateLogger<ChunkedUploadService>(),
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()),
            Microsoft.Extensions.Options.Options.Create(new FileSystemOptions()),
            TestFileVersioningSettings.From(new VersionRetentionOptions { MaxVersionCount = 0 }),
            wholeFile);
    }

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private async Task<(ChunkedUploadSession Session, FileChunk Chunk, byte[] Data)> SeedSessionAsync(
        FilesDbContext db, Guid userId, string fileName, string mimeType, byte[] data)
    {
        var hash = ContentHasher.ComputeHash(data);
        var storagePath = ContentHasher.GetChunkStoragePath(hash);
        await _engine.WriteChunkAsync(storagePath, data);

        // Mirrors UploadChunkAsync: a fresh chunk starts at reference count 0 and is incremented on completion.
        var chunk = new FileChunk { ChunkHash = hash, Size = data.Length, StoragePath = storagePath, ReferenceCount = 0 };
        db.FileChunks.Add(chunk);

        var session = new ChunkedUploadSession
        {
            FileName = fileName,
            TotalSize = data.Length,
            MimeType = mimeType,
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { hash }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);

        await db.SaveChangesAsync();
        return (session, chunk, data);
    }

    [TestMethod]
    public async Task CompleteUploadAsync_MediaFile_ConvertsToWholeFile()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (session, chunk, data) = await SeedSessionAsync(db, userId, "clip.mp4", "video/mp4", "video-bytes"u8.ToArray());

        var result = await CreateService(db).CompleteUploadAsync(session.Id, UserCaller(userId));

        var version = await db.FileVersions.AsNoTracking().SingleAsync(v => v.FileNodeId == result.Id);
        Assert.IsFalse(version.IsChunked, "Media should be stored as a whole-file blob at upload completion.");
        Assert.IsTrue(await _engine.ExistsAsync(version.StoragePath));
        Assert.AreEqual(0, await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version.Id));

        var reloadedChunk = await db.FileChunks.AsNoTracking().SingleAsync(c => c.Id == chunk.Id);
        Assert.AreEqual(0, reloadedChunk.ReferenceCount, "Chunk references are released once media is stored whole.");

        Assert.AreEqual(data.Length, version.Size);
    }

    [TestMethod]
    public async Task CompleteUploadAsync_DocumentFile_StaysChunked()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var (session, chunk, _) = await SeedSessionAsync(db, userId, "report.pdf", "application/pdf", "pdf-bytes"u8.ToArray());

        var result = await CreateService(db).CompleteUploadAsync(session.Id, UserCaller(userId));

        var version = await db.FileVersions.AsNoTracking().SingleAsync(v => v.FileNodeId == result.Id);
        Assert.IsTrue(version.IsChunked, "Documents keep the chunk pipeline.");
        Assert.AreEqual(1, await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version.Id));
        Assert.IsFalse(await _engine.ExistsAsync(version.StoragePath));

        var reloadedChunk = await db.FileChunks.AsNoTracking().SingleAsync(c => c.Id == chunk.Id);
        Assert.AreEqual(1, reloadedChunk.ReferenceCount);
    }
}
