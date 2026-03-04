using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class UploadSessionCleanupServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UploadSessionCleanupService CreateService(FilesDbContext db, IFileStorageEngine? storageEngine = null)
    {
        var storage = storageEngine ?? Mock.Of<IFileStorageEngine>();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(storage);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var provider = services.BuildServiceProvider();

        scope.Setup(s => s.ServiceProvider).Returns(provider);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new UploadSessionCleanupService(scopeFactory.Object, NullLogger<UploadSessionCleanupService>.Instance);
    }

    [TestMethod]
    public async Task CleanupAsync_ExpiredSession_MarksAsExpired()
    {
        using var db = CreateContext();

        var session = new ChunkedUploadSession
        {
            FileName = "expired.txt",
            TotalChunks = 1,
            ChunkManifest = "[]",
            UserId = Guid.NewGuid(),
            Status = UploadSessionStatus.InProgress,
            ExpiresAt = DateTime.UtcNow.AddHours(-2) // expired 2 hours ago
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CleanupAsync(CancellationToken.None);

        var updated = await db.UploadSessions.FindAsync(session.Id);
        Assert.AreEqual(UploadSessionStatus.Expired, updated!.Status);
    }

    [TestMethod]
    public async Task CleanupAsync_ActiveSession_NotExpired()
    {
        using var db = CreateContext();

        var session = new ChunkedUploadSession
        {
            FileName = "active.txt",
            TotalChunks = 1,
            ChunkManifest = "[]",
            UserId = Guid.NewGuid(),
            Status = UploadSessionStatus.InProgress,
            ExpiresAt = DateTime.UtcNow.AddHours(23) // still valid
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CleanupAsync(CancellationToken.None);

        var updated = await db.UploadSessions.FindAsync(session.Id);
        Assert.AreEqual(UploadSessionStatus.InProgress, updated!.Status);
    }

    [TestMethod]
    public async Task CleanupAsync_OrphanedChunk_DeletesFromStorageAndDb()
    {
        using var db = CreateContext();

        var chunk = new FileChunk
        {
            ChunkHash = "orphan_hash",
            StoragePath = "chunks/or/ph/orphan_hash",
            Size = 100,
            ReferenceCount = 0
        };
        db.FileChunks.Add(chunk);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        var service = CreateService(db, storageMock.Object);
        await service.CleanupAsync(CancellationToken.None);

        storageMock.Verify(s => s.DeleteAsync("chunks/or/ph/orphan_hash", It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(0, await db.FileChunks.CountAsync());
    }

    [TestMethod]
    public async Task CleanupAsync_ReferencedChunk_NotDeleted()
    {
        using var db = CreateContext();

        var chunk = new FileChunk
        {
            ChunkHash = "used_hash",
            StoragePath = "chunks/us/ed/used_hash",
            Size = 200,
            ReferenceCount = 1
        };
        db.FileChunks.Add(chunk);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        var service = CreateService(db, storageMock.Object);
        await service.CleanupAsync(CancellationToken.None);

        storageMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(1, await db.FileChunks.CountAsync());
    }

    [TestMethod]
    public async Task CleanupAsync_StorageDeleteFails_ContinuesWithOtherChunks()
    {
        using var db = CreateContext();

        var chunk1 = new FileChunk { ChunkHash = "fail_hash", StoragePath = "chunks/fa/il/fail_hash", Size = 100, ReferenceCount = 0 };
        var chunk2 = new FileChunk { ChunkHash = "ok_hash", StoragePath = "chunks/ok/ok/ok_hash", Size = 100, ReferenceCount = 0 };
        db.FileChunks.AddRange(chunk1, chunk2);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock.Setup(s => s.DeleteAsync("chunks/fa/il/fail_hash", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));
        storageMock.Setup(s => s.DeleteAsync("chunks/ok/ok/ok_hash", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, storageMock.Object);

        // Should not throw even when one chunk's storage delete fails
        await service.CleanupAsync(CancellationToken.None);

        // Both chunks removed from DB (best-effort: storage delete failure is logged, not blocking)
        Assert.AreEqual(0, await db.FileChunks.CountAsync());
    }
}
