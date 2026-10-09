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
/// Tests for <see cref="WholeFileStorageService"/> conversion using a real
/// <see cref="LocalFileStorageEngine"/> against a temporary directory.
/// </summary>
[TestClass]
public class WholeFileStorageServiceTests
{
    private string _basePath = null!;
    private LocalFileStorageEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-wholefile-{Guid.CreateVersion7():N}");
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

    private WholeFileStorageService CreateService(FilesDbContext db, bool wholeFileMediaStorage = true) =>
        new(db, _engine, Microsoft.Extensions.Options.Options.Create(new FileUploadOptions { WholeFileMediaStorage = wholeFileMediaStorage }),
            NullLogger<WholeFileStorageService>.Instance);

    private async Task<(FileNode Node, FileVersion Version, List<FileChunk> Chunks)> SeedChunkedVersionAsync(
        FilesDbContext db, string fileName, string? mimeType, bool writeBlobs, params byte[][] chunkDatas)
    {
        var owner = Guid.CreateVersion7();
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
            if (writeBlobs)
                await _engine.WriteChunkAsync(path, data);

            var chunk = new FileChunk { ChunkHash = hash, Size = data.Length, StoragePath = path };
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
            chunks[i].ReferenceCount = 1;
            db.FileVersionChunks.Add(new FileVersionChunk
            {
                FileVersionId = version.Id,
                FileChunkId = chunks[i].Id,
                SequenceIndex = i
            });
        }

        await db.SaveChangesAsync();
        return (node, version, chunks);
    }

    private async Task<byte[]> ReadBlobAsync(string storagePath)
    {
        await using var stream = await _engine.OpenReadStreamAsync(storagePath);
        Assert.IsNotNull(stream);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    [TestMethod]
    public async Task ConvertVersionToWholeFileAsync_MediaVersion_WritesBlobAndReleasesChunks()
    {
        using var db = CreateContext();
        var chunk1 = "hello "u8.ToArray();
        var chunk2 = "world"u8.ToArray();
        var (node, version, chunks) = await SeedChunkedVersionAsync(db, "clip.mp4", "video/mp4", writeBlobs: true, chunk1, chunk2);
        var service = CreateService(db);

        var converted = await service.ConvertVersionToWholeFileAsync(version.Id);

        Assert.IsTrue(converted);

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsFalse(reloaded.IsChunked);

        var blob = await ReadBlobAsync(version.StoragePath);
        CollectionAssert.AreEqual(chunk1.Concat(chunk2).ToArray(), blob);

        Assert.AreEqual(0, await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version.Id));
        foreach (var chunk in chunks)
        {
            var reloadedChunk = await db.FileChunks.AsNoTracking().SingleAsync(c => c.Id == chunk.Id);
            Assert.AreEqual(0, reloadedChunk.ReferenceCount);
        }

        Assert.IsNotNull(node);
    }

    [TestMethod]
    public async Task ConvertVersionToWholeFileAsync_SecondCall_IsNoOp()
    {
        using var db = CreateContext();
        var (_, version, _) = await SeedChunkedVersionAsync(db, "song.mp3", "audio/mpeg", writeBlobs: true, "data"u8.ToArray());
        var service = CreateService(db);

        Assert.IsTrue(await service.ConvertVersionToWholeFileAsync(version.Id));
        Assert.IsFalse(await service.ConvertVersionToWholeFileAsync(version.Id));
    }

    [TestMethod]
    public async Task ConvertVersionToWholeFileAsync_DocumentVersion_NotConverted()
    {
        using var db = CreateContext();
        var (_, version, _) = await SeedChunkedVersionAsync(db, "report.pdf", "application/pdf", writeBlobs: true, "pdf"u8.ToArray());
        var service = CreateService(db);

        Assert.IsFalse(await service.ConvertVersionToWholeFileAsync(version.Id));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsTrue(reloaded.IsChunked);
        Assert.AreEqual(1, await db.FileVersionChunks.CountAsync(vc => vc.FileVersionId == version.Id));
    }

    [TestMethod]
    public async Task ConvertVersionToWholeFileAsync_OptionDisabled_NotConverted()
    {
        using var db = CreateContext();
        var (_, version, _) = await SeedChunkedVersionAsync(db, "clip.mp4", "video/mp4", writeBlobs: true, "data"u8.ToArray());
        var service = CreateService(db, wholeFileMediaStorage: false);

        Assert.IsFalse(await service.ConvertVersionToWholeFileAsync(version.Id));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsTrue(reloaded.IsChunked);
    }

    [TestMethod]
    public async Task ConvertVersionToWholeFileAsync_MissingChunkBlob_LeavesVersionChunked()
    {
        using var db = CreateContext();
        var (_, version, _) = await SeedChunkedVersionAsync(db, "clip.mp4", "video/mp4", writeBlobs: false, "data"u8.ToArray());
        var service = CreateService(db);

        Assert.IsFalse(await service.ConvertVersionToWholeFileAsync(version.Id));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        Assert.IsTrue(reloaded.IsChunked);
        Assert.IsFalse(await _engine.ExistsAsync(version.StoragePath));

        // No scratch files left behind.
        var leftovers = Directory.EnumerateFiles(_basePath, "*.tmp-*", SearchOption.AllDirectories).ToList();
        Assert.AreEqual(0, leftovers.Count, $"Unexpected scratch files: {string.Join(", ", leftovers)}");
    }

    [TestMethod]
    public async Task ShouldStoreWholeFile_RespectsOptionGate()
    {
        using var db = CreateContext();
        var enabled = CreateService(db);
        var disabled = CreateService(db, wholeFileMediaStorage: false);

        Assert.IsTrue(enabled.ShouldStoreWholeFile("image/png", "photo.png"));
        Assert.IsFalse(enabled.ShouldStoreWholeFile("application/pdf", "doc.pdf"));
        Assert.IsFalse(disabled.ShouldStoreWholeFile("image/png", "photo.png"));
    }

    [TestMethod]
    public async Task OpenWholeFileStreamAsync_ReturnsBlobContent()
    {
        using var db = CreateContext();
        var (_, version, _) = await SeedChunkedVersionAsync(db, "photo.jpg", "image/jpeg", writeBlobs: true, "img"u8.ToArray());
        var service = CreateService(db);
        Assert.IsTrue(await service.ConvertVersionToWholeFileAsync(version.Id));

        var reloaded = await db.FileVersions.AsNoTracking().SingleAsync(v => v.Id == version.Id);
        await using var stream = await service.OpenWholeFileStreamAsync(reloaded);

        Assert.IsNotNull(stream);
        using var ms = new MemoryStream();
        await stream!.CopyToAsync(ms);
        CollectionAssert.AreEqual("img"u8.ToArray(), ms.ToArray());
    }
}
