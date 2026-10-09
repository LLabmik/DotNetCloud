using DotNetCloud.CLI.Infrastructure;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.CLI.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="StorageManifest"/> — the index that maps content-addressed blobs back to the
/// files that own them, so a backup of the storage tree can be interpreted without a live database.
/// </summary>
[TestClass]
public class StorageManifestTests
{
    private string _storageRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), $"dnc-manifest-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_storageRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private async Task WriteBlobAsync(string storagePath, int length)
    {
        var fullPath = Path.Combine(_storageRoot, storagePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, new byte[length]);
    }

    private static async Task<FileVersion> AddWholeFileVersionAsync(
        FilesDbContext db,
        string fileName,
        string storagePath,
        long size,
        bool deleted = false,
        string? mimeType = "video/mp4")
    {
        var owner = Guid.CreateVersion7();
        var node = new FileNode
        {
            Name = fileName,
            NodeType = FileNodeType.File,
            OwnerId = owner,
            MimeType = mimeType,
            Size = size,
            MaterializedPath = $"/Movies/{fileName}",
            IsDeleted = deleted,
            DeletedAt = deleted ? DateTime.UtcNow : null
        };
        db.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = size,
            ContentHash = $"{fileName}-hash",
            StoragePath = storagePath,
            MimeType = mimeType,
            CreatedByUserId = owner,
            IsChunked = false
        };
        db.FileVersions.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    private static async Task<FileVersion> AddChunkedVersionAsync(FilesDbContext db, string fileName, int chunkCount)
    {
        var owner = Guid.CreateVersion7();
        var node = new FileNode
        {
            Name = fileName,
            NodeType = FileNodeType.File,
            OwnerId = owner,
            MimeType = "application/pdf",
            Size = chunkCount * 4L,
            MaterializedPath = $"/Docs/{fileName}"
        };
        db.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = chunkCount * 4L,
            ContentHash = $"{fileName}-hash",
            StoragePath = $"files/{fileName}/blob",
            MimeType = "application/pdf",
            CreatedByUserId = owner,
            IsChunked = true
        };
        db.FileVersions.Add(version);

        // Insert the mappings in reverse so the test proves ordering comes from SequenceIndex.
        for (var i = chunkCount - 1; i >= 0; i--)
        {
            var chunk = new FileChunk
            {
                ChunkHash = $"{fileName}-chunk-{i}",
                Size = 4,
                StoragePath = $"chunks/aa/bb/{fileName}-chunk-{i}",
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

    private static string[] DataRows(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.StartsWith('#'))
            .ToArray();

    private async Task<(StorageManifest.Result Result, string Output)> WriteAsync(FilesDbContext db, bool includeChunks = true)
    {
        using var writer = new StringWriter();
        var result = await StorageManifest.WriteAsync(db, _storageRoot, includeChunks, writer);
        return (result, writer.ToString());
    }

    [TestMethod]
    public async Task WriteAsync_WholeFileVersion_EmitsFileRowAndVerifiesBlob()
    {
        using var db = CreateContext();
        const string storagePath = "files/ae/39/ae395195dc7daec31457fb9d7787fb0bc9b5af21a49751fa8b9068b3a6f9a0dd";
        await WriteBlobAsync(storagePath, 128);
        await AddWholeFileVersionAsync(db, "20261005_225022.mp4", storagePath, 128);

        var (result, output) = await WriteAsync(db);

        Assert.AreEqual(1, result.FileVersions);
        Assert.AreEqual(0, result.MissingBlobs);
        Assert.AreEqual(0, result.TruncatedBlobs);
        Assert.AreEqual(128L, result.TotalBytes);

        var row = DataRows(output).Single();
        var columns = row.Split('\t');
        Assert.AreEqual("file", columns[0]);
        Assert.AreEqual(storagePath, columns[3]);
        Assert.AreEqual("128", columns[4]);
        Assert.AreEqual("video/mp4", columns[5]);
        Assert.AreEqual("20261005_225022.mp4", columns[6]);
        Assert.AreEqual("/Movies/20261005_225022.mp4", columns[7]);
        Assert.AreEqual("false", columns[9]);
        Assert.AreEqual("false", columns[10]);
    }

    [TestMethod]
    public async Task WriteAsync_HeaderDescribesTheStorageLayout()
    {
        using var db = CreateContext();
        await WriteBlobAsync("files/aa/bb/blob", 4);
        await AddWholeFileVersionAsync(db, "clip.mp4", "files/aa/bb/blob", 4);

        var (_, output) = await WriteAsync(db);

        StringAssert.Contains(output, "#columns\t");
        StringAssert.Contains(output, "storagePath");
        StringAssert.Contains(output, "sequenceIndex");
        StringAssert.Contains(output, "whole-file blob at storagePath");
    }

    [TestMethod]
    public async Task WriteAsync_BlobMissingFromDisk_IsCounted()
    {
        using var db = CreateContext();
        await AddWholeFileVersionAsync(db, "lost.mp4", "files/aa/bb/absent", 4096);

        var (result, output) = await WriteAsync(db);

        Assert.AreEqual(1, result.MissingBlobs);
        Assert.AreEqual(0, result.TruncatedBlobs);
        StringAssert.Contains(output, "missingWholeFileBlobs=1");
    }

    [TestMethod]
    public async Task WriteAsync_BlobWithUnexpectedSize_IsCounted()
    {
        using var db = CreateContext();
        const string storagePath = "files/aa/bb/truncated";
        await WriteBlobAsync(storagePath, 10);
        await AddWholeFileVersionAsync(db, "truncated.mp4", storagePath, 4096);

        var (result, output) = await WriteAsync(db);

        Assert.AreEqual(0, result.MissingBlobs);
        Assert.AreEqual(1, result.TruncatedBlobs);
        StringAssert.Contains(output, "truncatedWholeFileBlobs=1");
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedVersion_EmitsChunkRowsInSequenceOrder()
    {
        using var db = CreateContext();
        var version = await AddChunkedVersionAsync(db, "report.pdf", chunkCount: 3);

        var (result, output) = await WriteAsync(db);

        Assert.AreEqual(1, result.FileVersions);
        Assert.AreEqual(3, result.ChunkRows);

        var blobRows = DataRows(output).Where(r => r.StartsWith("blob\t", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(3, blobRows.Length);
        CollectionAssert.AreEqual(
            new[] { "chunks/aa/bb/report.pdf-chunk-0", "chunks/aa/bb/report.pdf-chunk-1", "chunks/aa/bb/report.pdf-chunk-2" },
            blobRows.Select(r => r.Split('\t')[3]).ToArray());

        // Every chunk row points back at its version, and carries its sequence index last.
        foreach (var row in blobRows)
        {
            var columns = row.Split('\t');
            Assert.AreEqual(version.Id.ToString().ToUpperInvariant(), columns[1].ToUpperInvariant());
            Assert.AreEqual("4", columns[4]);
        }

        Assert.AreEqual("0", blobRows[0].Split('\t')[11]);
        Assert.AreEqual("2", blobRows[2].Split('\t')[11]);
    }

    [TestMethod]
    public async Task WriteAsync_NoChunks_OmitsChunkRows()
    {
        using var db = CreateContext();
        await AddChunkedVersionAsync(db, "report.pdf", chunkCount: 2);

        var (result, output) = await WriteAsync(db, includeChunks: false);

        Assert.AreEqual(1, result.FileVersions);
        Assert.AreEqual(0, result.ChunkRows);
        Assert.IsFalse(DataRows(output).Any(r => r.StartsWith("blob\t", StringComparison.Ordinal)),
            "Chunk rows must be omitted when the caller opts out.");
        StringAssert.Contains(output, "chunk rows omitted");
    }

    [TestMethod]
    public async Task WriteAsync_TrashedNode_IsStillListedAndFlagged()
    {
        using var db = CreateContext();
        await WriteBlobAsync("files/aa/bb/trashed", 8);
        await AddWholeFileVersionAsync(db, "trashed.mp4", "files/aa/bb/trashed", 8, deleted: true);

        var (result, output) = await WriteAsync(db);

        Assert.AreEqual(1, result.FileVersions, "Trashed content still occupies storage, so it belongs in the manifest.");
        Assert.AreEqual("true", DataRows(output).Single().Split('\t')[10]);
    }

    [TestMethod]
    public async Task WriteAsync_FileNameWithTabsOrNewlines_IsSanitized()
    {
        using var db = CreateContext();
        await WriteBlobAsync("files/aa/bb/odd", 4);
        await AddWholeFileVersionAsync(db, "weird\tname\nhere.mp4", "files/aa/bb/odd", 4);

        var (_, output) = await WriteAsync(db);

        Assert.AreEqual(1, DataRows(output).Length, "A file name must not be able to inject an extra row.");
        StringAssert.Contains(output, "weird name here.mp4");
    }
}
