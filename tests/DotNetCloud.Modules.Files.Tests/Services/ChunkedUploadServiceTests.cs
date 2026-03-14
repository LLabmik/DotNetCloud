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
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()),
            Microsoft.Extensions.Options.Options.Create(new FileSystemOptions()));
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

    [TestMethod]
    public async Task CompleteUploadAsync_WithCdcChunkSizes_StoresOffsetAndSizeOnVersionChunks()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();

        const int chunk1Size = 524288; // 512 KB
        const int chunk2Size = 786432; // 768 KB
        var hash1 = "deadbeef" + new string('1', 56); // fake but well-formed 64-char hex
        var hash2 = "cafebabe" + new string('2', 56);

        db.FileChunks.Add(new FileChunk { ChunkHash = hash1, StoragePath = $"chunks/de/ad/{hash1}", Size = chunk1Size });
        db.FileChunks.Add(new FileChunk { ChunkHash = hash2, StoragePath = $"chunks/ca/fe/{hash2}", Size = chunk2Size });

        var session = new ChunkedUploadSession
        {
            FileName = "cdc-file.bin",
            TotalSize = chunk1Size + chunk2Size,
            TotalChunks = 2,
            ReceivedChunks = 2,
            ChunkManifest = JsonSerializer.Serialize(new[] { hash1, hash2 }),
            ChunkSizesManifest = JsonSerializer.Serialize(new[] { chunk1Size, chunk2Size }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        var version = await db.FileVersions.FirstAsync();
        var versionChunks = await db.FileVersionChunks
            .Where(vc => vc.FileVersionId == version.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync();

        Assert.AreEqual(2, versionChunks.Count);

        Assert.AreEqual(0L, versionChunks[0].Offset);
        Assert.AreEqual(chunk1Size, versionChunks[0].ChunkSize);

        Assert.AreEqual((long)chunk1Size, versionChunks[1].Offset);
        Assert.AreEqual(chunk2Size, versionChunks[1].ChunkSize);
    }

    [TestMethod]
    public async Task InitiateUploadAsync_WithCdcChunkSizes_StoresSizesManifestOnSession()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10_000_000, UsedBytes = 0 });
        await db.SaveChangesAsync();

        var chunkSizes = new[] { 524288, 786432 };
        var service = CreateService(db);
        await service.InitiateUploadAsync(new InitiateUploadDto
        {
            FileName = "cdc.bin",
            TotalSize = chunkSizes.Sum(),
            ChunkHashes = ["hash-a", "hash-b"],
            ChunkSizes = chunkSizes
        }, UserCaller(userId));

        var session = await db.UploadSessions.FirstAsync();
        Assert.IsNotNull(session.ChunkSizesManifest);
        var stored = JsonSerializer.Deserialize<int[]>(session.ChunkSizesManifest)!;
        CollectionAssert.AreEqual(chunkSizes, stored);
    }

    [TestMethod]
    public async Task InitiateUploadAsync_WithoutCdcChunkSizes_LeavesManifestNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10_000_000, UsedBytes = 0 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.InitiateUploadAsync(new InitiateUploadDto
        {
            FileName = "legacy.bin",
            TotalSize = 1024,
            ChunkHashes = ["hash-x"]
        }, UserCaller(userId));

        var session = await db.UploadSessions.FirstAsync();
        Assert.IsNull(session.ChunkSizesManifest, "Legacy uploads must not have a ChunkSizesManifest.");
    }

    [TestMethod]
    public async Task CompleteUploadAsync_NewFile_CaseInsensitiveSiblingExists_ThrowsNameConflictException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "aabbccdd1122";

        // Existing sibling with different casing
        var parent = new FileNode
        {
            Name = "MyFolder",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            Depth = 0
        };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);
        db.FileNodes.Add(new FileNode
        {
            Name = "Report.pdf",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ParentId = parent.Id
        });

        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = "chunks/aa/bb/aabbccdd1122", Size = 50 });

        var session = new ChunkedUploadSession
        {
            FileName = "report.pdf",   // lowercase — case-insensitive conflict with "Report.pdf"
            TotalSize = 50,
            MimeType = "application/pdf",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = System.Text.Json.JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            TargetParentId = parent.Id,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.NameConflictException>(
            () => service.CompleteUploadAsync(session.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task CompleteUploadAsync_NewRootFile_ExactNameExists_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "rootdupe001";

        db.FileNodes.Add(new FileNode
        {
            Name = "BenK Toy Package.png",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 10
        });
        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = "chunks/ro/ot/rootdupe001", Size = 50 });

        var session = new ChunkedUploadSession
        {
            FileName = "BenK Toy Package.png",
            TotalSize = 50,
            MimeType = "image/png",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CompleteUploadAsync(session.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task CompleteUploadAsync_NewChildFile_ExactNameExists_ThrowsValidationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "childdupe001";

        var parent = new FileNode
        {
            Name = "Clients",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            Depth = 0
        };
        parent.MaterializedPath = $"/{parent.Id}";
        db.FileNodes.Add(parent);
        db.FileNodes.Add(new FileNode
        {
            Name = "report.pdf",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            ParentId = parent.Id,
            Size = 10
        });
        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = "chunks/ch/il/childdupe001", Size = 50 });

        var session = new ChunkedUploadSession
        {
            FileName = "report.pdf",
            TotalSize = 50,
            MimeType = "application/pdf",
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

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.CompleteUploadAsync(session.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task InitiateUploadAsync_WithPosixMode_StoresPosixModeOnSession()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10_000_000, UsedBytes = 0 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.InitiateUploadAsync(new InitiateUploadDto
        {
            FileName = "script.sh",
            TotalSize = 512,
            ChunkHashes = ["hashA"],
            PosixMode = 493,
            PosixOwnerHint = "alice:developers"
        }, UserCaller(userId));

        var session = await db.UploadSessions.FirstAsync();
        Assert.AreEqual(493, session.PosixMode);
        Assert.AreEqual("alice:developers", session.PosixOwnerHint);
    }

    [TestMethod]
    public async Task CompleteUploadAsync_NewFile_WithPosixMode_StoresPosixModeOnNodeAndVersion()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "posixhash001";

        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = $"chunks/po/si/{chunkHash}", Size = 256 });

        var session = new ChunkedUploadSession
        {
            FileName = "deploy.sh",
            TotalSize = 256,
            MimeType = "text/x-shellscript",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            Status = UploadSessionStatus.InProgress,
            PosixMode = 493,
            PosixOwnerHint = "deploy:ops"
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        Assert.AreEqual(493, result.PosixMode);
        Assert.AreEqual("deploy:ops", result.PosixOwnerHint);

        var node = await db.FileNodes.FindAsync(result.Id);
        Assert.IsNotNull(node);
        Assert.AreEqual(493, node.PosixMode);
        Assert.AreEqual("deploy:ops", node.PosixOwnerHint);

        var version = await db.FileVersions.FirstAsync(v => v.FileNodeId == result.Id);
        Assert.AreEqual(493, version.PosixMode);
    }

    [TestMethod]
    public async Task CompleteUploadAsync_ReUpload_NullPosixModePreservesExistingPosixMode()
    {
        // Windows client re-uploads a file originally uploaded by Linux — PosixMode must survive.
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "posixhash002";

        var existingNode = new FileNode
        {
            Name = "config.json",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 100,
            ContentHash = "oldhash",
            StoragePath = "files/ol/dh/oldhash",
            Depth = 0,
            PosixMode = 416,
            PosixOwnerHint = "bob:staff"
        };
        existingNode.MaterializedPath = $"/{existingNode.Id}";
        db.FileNodes.Add(existingNode);
        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = $"chunks/po/si/{chunkHash}", Size = 200 });

        // Session has null PosixMode — simulates a Windows client upload
        var session = new ChunkedUploadSession
        {
            FileName = "config.json",
            TotalSize = 200,
            MimeType = "application/json",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            TargetFileNodeId = existingNode.Id,
            Status = UploadSessionStatus.InProgress,
            PosixMode = null,
            PosixOwnerHint = null
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        Assert.AreEqual(416, result.PosixMode, "PosixMode must be preserved when Windows client sends null.");
        Assert.AreEqual("bob:staff", result.PosixOwnerHint, "PosixOwnerHint must be preserved when Windows client sends null.");
    }

    [TestMethod]
    public async Task CompleteUploadAsync_ReUpload_NewPosixModeUpdatesExistingPosixMode()
    {
        // Linux client re-uploads with changed permissions — server must update the stored PosixMode.
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var chunkHash = "posixhash003";

        var existingNode = new FileNode
        {
            Name = "run.sh",
            NodeType = FileNodeType.File,
            OwnerId = userId,
            Size = 512,
            ContentHash = "oldhash2",
            StoragePath = "files/ol/dh/oldhash2",
            Depth = 0,
            PosixMode = 420,
            PosixOwnerHint = "alice:staff"
        };
        existingNode.MaterializedPath = $"/{existingNode.Id}";
        db.FileNodes.Add(existingNode);
        db.FileChunks.Add(new FileChunk { ChunkHash = chunkHash, StoragePath = $"chunks/po/si/{chunkHash}", Size = 600 });

        var session = new ChunkedUploadSession
        {
            FileName = "run.sh",
            TotalSize = 600,
            MimeType = "text/x-shellscript",
            TotalChunks = 1,
            ReceivedChunks = 1,
            ChunkManifest = JsonSerializer.Serialize(new[] { chunkHash }),
            UserId = userId,
            TargetFileNodeId = existingNode.Id,
            Status = UploadSessionStatus.InProgress,
            PosixMode = 493,   // Linux client changed permissions to executable
            PosixOwnerHint = "alice:staff"
        };
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CompleteUploadAsync(session.Id, UserCaller(userId));

        Assert.AreEqual(493, result.PosixMode, "PosixMode must be updated when Linux client sends a new value.");
        Assert.AreEqual("alice:staff", result.PosixOwnerHint);

        var version = await db.FileVersions.FirstAsync(v => v.FileNodeId == result.Id);
        Assert.AreEqual(493, version.PosixMode, "FileVersion must capture the updated PosixMode.");
    }
}
