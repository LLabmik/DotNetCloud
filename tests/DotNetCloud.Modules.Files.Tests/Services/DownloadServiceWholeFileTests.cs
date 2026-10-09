using System.IO.Compression;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for the whole-file branches of <see cref="DownloadService"/>: direct blob streaming,
/// lazy conversion of chunked media, unchanged document behaviour, and ZIP/auto-repair paths.
/// </summary>
[TestClass]
public class DownloadServiceWholeFileTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-download-wholefile-{Guid.CreateVersion7():N}");
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

    private DownloadService CreateService(FilesDbContext db)
    {
        var wholeFile = new WholeFileStorageService(db, _engine,
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()), NullLogger<WholeFileStorageService>.Instance);
        return new DownloadService(db, _engine, NullLogger<DownloadService>.Instance, new PermissionService(db),
            Microsoft.Extensions.Options.Options.Create(new FileUploadOptions()), shareAccessMembershipResolver: null, wholeFileStorageService: wholeFile);
    }

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    private async Task<(FileNode Node, FileVersion Version)> SeedChunkedVersionAsync(
        FilesDbContext db, Guid owner, string fileName, string? mimeType, params byte[][] chunkDatas)
    {
        var node = new FileNode
        {
            Name = fileName,
            NodeType = FileNodeType.File,
            OwnerId = owner,
            MimeType = mimeType,
            Size = chunkDatas.Sum(d => (long)d.Length)
        };
        db.FileNodes.Add(node);

        var hashes = new List<string>();
        var chunks = new List<FileChunk>();
        foreach (var data in chunkDatas)
        {
            var hash = ContentHasher.ComputeHash(data);
            hashes.Add(hash);
            var path = ContentHasher.GetChunkStoragePath(hash);
            await _engine.WriteChunkAsync(path, data);
            var chunk = new FileChunk { ChunkHash = hash, Size = data.Length, StoragePath = path, ReferenceCount = 1 };
            db.FileChunks.Add(chunk);
            chunks.Add(chunk);
        }

        var manifestHash = ContentHasher.ComputeManifestHash(hashes);
        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = node.Size,
            ContentHash = manifestHash,
            StoragePath = ContentHasher.GetFileStoragePath(manifestHash),
            MimeType = mimeType,
            CreatedByUserId = owner
        };
        db.FileVersions.Add(version);

        for (var i = 0; i < chunks.Count; i++)
        {
            db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = version.Id,
                FileChunkId = chunks[i].Id,
                SequenceIndex = i
            });
        }

        await db.SaveChangesAsync();
        return (node, version);
    }

    private static async Task<string> ReadAllAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_ChunkedMedia_ConvertsOnFirstReadAndServesBlob()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var (node, version) = await SeedChunkedVersionAsync(db, owner, "clip.mp4", "video/mp4",
            "first-"u8.ToArray(), "second"u8.ToArray());

        var service = CreateService(db);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(owner));
        Assert.AreEqual("first-second", await ReadAllAsync(stream));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsFalse(reloaded.IsChunked, "First read should lazily convert chunked media to whole-file.");
        Assert.IsTrue(await _engine.ExistsAsync(version.StoragePath));
        Assert.AreEqual(0, await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version.Id));
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_WholeFileVersion_StreamsBlobDirectly()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        const string storagePath = "files/ab/cd/wholefileblob";
        await _engine.WriteChunkAsync(storagePath, "whole-file-content"u8.ToArray());

        var node = new FileNode { Name = "movie.mp4", NodeType = FileNodeType.File, OwnerId = owner, MimeType = "video/mp4", Size = 17 };
        db.FileNodes.Add(node);
        db.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 17,
            ContentHash = "wholefileblob",
            StoragePath = storagePath,
            MimeType = "video/mp4",
            CreatedByUserId = owner,
            IsChunked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(owner));
        Assert.IsTrue(stream.CanSeek, "Whole-file streams must be seekable for HTTP range requests.");
        Assert.AreEqual("whole-file-content", await ReadAllAsync(stream));
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_ChunkedDocument_StaysChunked()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var (node, version) = await SeedChunkedVersionAsync(db, owner, "report.pdf", "application/pdf",
            "pdf-"u8.ToArray(), "content"u8.ToArray());

        var service = CreateService(db);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(owner));
        Assert.AreEqual("pdf-content", await ReadAllAsync(stream));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsTrue(reloaded.IsChunked, "Documents are never converted to whole-file storage.");
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_WholeFileBlobMissing_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var node = new FileNode { Name = "movie.mp4", NodeType = FileNodeType.File, OwnerId = owner, MimeType = "video/mp4", Size = 5 };
        db.FileNodes.Add(node);
        db.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 5,
            ContentHash = "missingblob",
            StoragePath = "files/ab/cd/missingblob",
            MimeType = "video/mp4",
            CreatedByUserId = owner,
            IsChunked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.DownloadCurrentAsync(node.Id, UserCaller(owner)));
    }

    [TestMethod]
    public async Task DownloadZipAsync_WholeFileEntry_UsesBlobContent()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        const string storagePath = "files/ab/cd/zipblob";
        await _engine.WriteChunkAsync(storagePath, "zip-whole-file"u8.ToArray());

        var node = new FileNode { Name = "video.mp4", NodeType = FileNodeType.File, OwnerId = owner, MimeType = "video/mp4", Size = 14 };
        db.FileNodes.Add(node);
        db.FileVersions.Add(new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 14,
            ContentHash = "zipblob",
            StoragePath = storagePath,
            MimeType = "video/mp4",
            CreatedByUserId = owner,
            IsChunked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await using var zipStream = await service.DownloadZipAsync([node.Id], UserCaller(owner));
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.Single(e => e.Name == "video.mp4");
        using var entryStream = entry.Open();
        Assert.AreEqual("zip-whole-file", await ReadAllAsync(entryStream));
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_MissingVersionButWholeFileBlobExists_AutoRepairs()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        const string storagePath = "files/ab/cd/autorepair";
        await _engine.WriteChunkAsync(storagePath, "repaired-content"u8.ToArray());

        // Node without any FileVersion row, but the whole-file blob exists at its StoragePath.
        var node = new FileNode
        {
            Name = "orphan.mp4",
            NodeType = FileNodeType.File,
            OwnerId = owner,
            MimeType = "video/mp4",
            Size = 16,
            ContentHash = "autorepair",
            StoragePath = storagePath,
            CurrentVersion = 1
        };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(owner));
        Assert.AreEqual("repaired-content", await ReadAllAsync(stream));

        var repairedVersion = await db.FileVersions.AsNoTracking().SingleAsync(v => v.FileNodeId == node.Id);
        Assert.IsFalse(repairedVersion.IsChunked);
        Assert.AreEqual(storagePath, repairedVersion.StoragePath);
    }

    [TestMethod]
    public async Task DownloadCurrentAsync_WholeFileBlobMissingButChunksRemain_RebuildsFromChunks()
    {
        using var db = CreateContext();
        var owner = Guid.CreateVersion7();
        var (node, version) = await SeedChunkedVersionAsync(db, owner, "clip.mp4", "video/mp4",
            "alpha-"u8.ToArray(), "omega"u8.ToArray());

        // Simulate a bad conversion: the version is flagged whole-file and its blob is gone, but the
        // chunk mappings survive — so the content is still recoverable and the read must not fail.
        var versionRow = await db.FileVersions.SingleAsync(v => v.Id == version.Id);
        versionRow.IsChunked = false;
        await db.SaveChangesAsync();
        Assert.IsFalse(await _engine.ExistsAsync(version.StoragePath));

        var service = CreateService(db);

        await using var stream = await service.DownloadCurrentAsync(node.Id, UserCaller(owner));
        Assert.AreEqual("alpha-omega", await ReadAllAsync(stream));
    }
}
