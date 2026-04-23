using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
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

        // PlainTextExtractor handles text/plain
        var plainTextExtractor = new TestPlainTextExtractor();
        var module = new FilesSearchableModule(db, storageMock.Object, new IContentExtractor[] { plainTextExtractor }, NullLogger<FilesSearchableModule>.Instance);

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
        var module = new FilesSearchableModule(db, storageMock.Object, Array.Empty<IContentExtractor>(), NullLogger<FilesSearchableModule>.Instance);

        var doc = await module.GetSearchableDocumentAsync(node.Id.ToString());

        Assert.IsNotNull(doc);
        Assert.AreEqual("Photo.png", doc.Content);
    }

    [TestMethod]
    public async Task GetAllSearchableDocumentsAsync_AdminSharedFolder_IncludesMountedFilesWithGroupVisibilityMetadata()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tempPath = Path.Combine(Path.GetTempPath(), $"dnc-search-mounted-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempPath);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempPath, "readme.txt"), "mounted search content");

            db.AdminSharedFolders.Add(new AdminSharedFolderDefinition
            {
                DisplayName = "Mounted Library",
                SourcePath = tempPath,
                CreatedByUserId = ownerId,
                Grants = [new AdminSharedFolderGrant { GroupId = groupId }],
            });
            await db.SaveChangesAsync();

            var module = new FilesSearchableModule(db, Mock.Of<IFileStorageEngine>(), new IContentExtractor[] { new TestPlainTextExtractor() }, NullLogger<FilesSearchableModule>.Instance);

            var documents = await module.GetAllSearchableDocumentsAsync();
            var mountedDocument = documents.Single(document => document.Title == "readme.txt");

            Assert.AreEqual("files", mountedDocument.ModuleId);
            Assert.AreEqual("AdminSharedFolderMount", mountedDocument.EntityType);
            Assert.AreEqual(SearchVisibilityMetadata.VisibilityScopeGroupMembers, mountedDocument.Metadata[SearchVisibilityMetadata.VisibilityScopeKey]);
            Assert.AreEqual(SearchVisibilityMetadata.BuildGroupScopeKey([groupId]), mountedDocument.Metadata[SearchVisibilityMetadata.GroupScopeKey]);
            Assert.AreEqual("readme.txt", mountedDocument.Metadata[SearchVisibilityMetadata.RelativePathKey]);
            Assert.IsTrue(mountedDocument.Content.Contains("mounted search content", StringComparison.Ordinal));
            Assert.IsTrue(mountedDocument.Content.Contains("_DotNetCloud/Mounted Library/readme.txt", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    /// <summary>Simple text extractor for unit tests — returns stream content as-is.</summary>
    private sealed class TestPlainTextExtractor : IContentExtractor
    {
        public bool CanExtract(string mimeType) =>
            mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

        public async Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(fileStream, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return new ExtractedContent { Text = text, Metadata = new Dictionary<string, string>() };
        }
    }
}
