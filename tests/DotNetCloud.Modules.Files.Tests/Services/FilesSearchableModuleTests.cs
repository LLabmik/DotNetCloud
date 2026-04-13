using System.Text;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class FilesSearchableModuleTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    [TestMethod]
    public async Task GetSearchableDocumentAsync_TextFile_IncludesExtractedBodyContent()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var node = new FileNode
        {
            Name = "TestFile.txt",
            NodeType = FileNodeType.File,
            OwnerId = ownerId,
            MimeType = "text/plain"
        };

        db.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 64,
            ContentHash = "hash-a",
            StoragePath = "files/aa/bb/hash-a",
            CreatedByUserId = ownerId,
            MimeType = "text/plain"
        };
        db.FileVersions.Add(version);

        var chunk = new FileChunk
        {
            ChunkHash = "chunk-a",
            StoragePath = "chunks/test/path",
            Size = 64
        };
        db.FileChunks.Add(chunk);

        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });

        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>();
        storageMock
            .Setup(s => s.OpenReadStreamAsync("chunks/test/path", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("This is the first version. This is the second version.")));

        var module = new FilesSearchableModule(db, storageMock.Object, NullLogger<FilesSearchableModule>.Instance);

        var doc = await module.GetSearchableDocumentAsync(node.Id.ToString());

        Assert.IsNotNull(doc);
        Assert.IsTrue(doc.Content.Contains("version", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(doc.Content.Contains("TestFile.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetSearchableDocumentAsync_BinaryFile_DoesNotAttemptBodyExtraction()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var node = new FileNode
        {
            Name = "Photo.png",
            NodeType = FileNodeType.File,
            OwnerId = ownerId,
            MimeType = "image/png"
        };

        db.FileNodes.Add(node);

        var version = new FileVersion
        {
            FileNodeId = node.Id,
            VersionNumber = 1,
            Size = 128,
            ContentHash = "hash-image",
            StoragePath = "files/ff/ee/hash-image",
            CreatedByUserId = ownerId,
            MimeType = "image/png"
        };
        db.FileVersions.Add(version);

        var chunk = new FileChunk
        {
            ChunkHash = "chunk-image",
            StoragePath = "chunks/image/path",
            Size = 128
        };
        db.FileChunks.Add(chunk);

        db.FileVersionChunks.Add(new FileVersionChunk
        {
            FileVersionId = version.Id,
            FileChunkId = chunk.Id,
            SequenceIndex = 0
        });

        await db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorageEngine>(MockBehavior.Strict);
        var module = new FilesSearchableModule(db, storageMock.Object, NullLogger<FilesSearchableModule>.Instance);

        var doc = await module.GetSearchableDocumentAsync(node.Id.ToString());

        Assert.IsNotNull(doc);
        Assert.AreEqual("Photo.png", doc.Content);
    }
}
