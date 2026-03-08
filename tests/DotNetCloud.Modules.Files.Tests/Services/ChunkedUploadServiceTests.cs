using System.Text;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class ChunkedUploadServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static ChunkedUploadService CreateService(
        FilesDbContext db,
        IFileStorageEngine? storageEngine = null,
        IQuotaService? quotaService = null)
    {
        var storageMock = storageEngine ?? Mock.Of<IFileStorageEngine>();
        var quotaMock = quotaService ?? CreateMockQuotaService(true);
        return new ChunkedUploadService(
            db, storageMock, quotaMock,
            Mock.Of<IEventBus>(), NullLoggerFactory.Instance.CreateLogger<ChunkedUploadService>(),
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()));
    }

    private static IQuotaService CreateMockQuotaService(bool hasSufficientQuota)
    {
        var mock = new Mock<IQuotaService>();
        mock.Setup(q => q.HasSufficientQuotaAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasSufficientQuota);
        return mock.Object;
    }

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    [TestMethod]
    public async Task InitiateUploadAsync_WithDedup_IdentifiesExistingChunks()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        // Pre-existing chunk
        db.FileChunks.Add(new FileChunk { ChunkHash = "hash1", StoragePath = "chunks/ha/sh/hash1", Size = 100 });
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 0 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.InitiateUploadAsync(new InitiateUploadDto
        {
            FileName = "test.txt",
            TotalSize = 200,
            ChunkHashes = ["hash1", "hash2"]
        }, UserCaller(userId));

        Assert.AreNotEqual(Guid.Empty, result.SessionId);
        Assert.AreEqual(1, result.ExistingChunks.Count);
        Assert.AreEqual(1, result.MissingChunks.Count);
        Assert.IsTrue(result.ExistingChunks.Contains("hash1"));
        Assert.IsTrue(result.MissingChunks.Contains("hash2"));
    }

    [TestMethod]
    public async Task InitiateUploadAsync_InsufficientQuota_ThrowsValidationException()
    {
        using var db = CreateContext();
        var service = CreateService(db, quotaService: CreateMockQuotaService(false));

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.InitiateUploadAsync(new InitiateUploadDto
            {
                FileName = "big.bin",
                TotalSize = 999999999,
                ChunkHashes = ["hash1"]
            }, UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task UploadChunkAsync_CorrectHash_StoresChunk()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var data = Encoding.UTF8.GetBytes("hello world");
        var hash = ContentHasher.ComputeHash(data);

        var session = new ChunkedUploadSession
        {
            FileName = "test.txt",
            TotalChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { hash }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        var service = CreateService(db, storageMock.Object);

        await service.UploadChunkAsync(session.Id, hash, data, UserCaller(userId));

        var chunk = await db.FileChunks.FirstOrDefaultAsync(c => c.ChunkHash == hash);
        Assert.IsNotNull(chunk);
        storageMock.Verify(s => s.WriteChunkAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadChunkAsync_HashMismatch_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var session = new ChunkedUploadSession
        {
            FileName = "test.txt",
            TotalChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { "declared_hash" }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var data = Encoding.UTF8.GetBytes("data");

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.UploadChunkAsync(session.Id, "declared_hash", data, UserCaller(userId)));
    }

    [TestMethod]
    public async Task CompleteUploadAsync_AllChunksPresent_CreatesFileAndVersion()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "abc123def456";

        // Create parent folder
        var parent = new FileNode
        {
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            Depth = 0
        };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);

        // Create chunk
        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = "chunks/ab/c1/abc123def456", Size = 100 });

        // Create session
        var session = new ChunkedUploadSession
        {
            FileName = "uploaded.txt",
            TotalSize = 100,
            MimeType = "text/plain",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            TargetParentId = parent.Id,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        Assert.AreEqual("uploaded.txt", result.Name);
        Assert.AreEqual(100, result.Size);
        Assert.AreEqual("text/plain", result.MimeType);
        Assert.AreEqual(parent.Id, result.ParentId);

        // Verify version was created
        var version = await db.FileVersions.FirstOrDefaultAsync(v => v.FileNodeId == result.Id);
        Assert.IsNotNull(version);
        Assert.AreEqual(1, version.VersionNumber);

        // Verify chunk refcount incremented
        var chunk = await db.FileChunks.FirstAsync(c => c.ChunkHash == chunkHash);
        Assert.AreEqual(2, chunk.ReferenceCount); // Was 1, now incremented
    }

    [TestMethod]
    public async Task CancelUploadAsync_ActiveSession_CancelsSuccessfully()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var session = new ChunkedUploadSession
        {
            FileName = "cancel-me.txt",
            TotalChunks = 1,
            ChunkManifest = "[]",
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CancelUploadAsync(session.Id, UserCaller(userId));

        var updated = await db.UploadSessions.FindAsync(session.Id);
        Assert.AreEqual(UploadSessionStatus.Cancelled, updated!.Status);
    }

    [TestMethod]
    public async Task CompleteUploadAsync_ZeroByteManifest_CreatesEmptyFile()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        var session = new ChunkedUploadSession
        {
            FileName = "empty.docx",
            TotalSize = 0,
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TotalChunks = 0,
            ReceivedChunks = 0,
            ChunkManifest = "[]",
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        Assert.AreEqual("empty.docx", result.Name);
        Assert.AreEqual(0, result.Size);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.MimeType);

        var version = await db.FileVersions.FirstOrDefaultAsync(v => v.FileNodeId == result.Id);
        Assert.IsNotNull(version);
        Assert.AreEqual(0, version.Size);

        var versionChunkCount = await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version!.Id);
        Assert.AreEqual(0, versionChunkCount);
    }

    [TestMethod]
    public async Task GetSessionAsync_WrongUser_ReturnsNull()
    {
        using var db = CreateContext();
        var session = new ChunkedUploadSession
        {
            FileName = "private.txt",
            TotalChunks = 1,
            ChunkManifest = "[]",
            UserId = Guid.NewGuid(),
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetSessionAsync(session.Id, UserCaller(Guid.NewGuid()));

        Assert.IsNull(result);
    }
}
