using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SqlServerSearchProvider"/> using InMemory database.
/// The SqlServer provider falls back to Contains() which works with InMemory.
/// </summary>
[TestClass]
public class SqlServerSearchProviderTests
{
    private SearchDbContext _db = null!;
    private SqlServerSearchProvider _provider = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SearchDbContext(options);
        _provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        _userId = Guid.NewGuid();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    #region IndexDocumentAsync

    [TestMethod]
    public async Task IndexDocumentAsync_NewDocument_CreatesEntry()
    {
        var doc = CreateDocument("files", "entity-1", "FileNode", "Test File");

        await _provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstOrDefaultAsync();
        Assert.IsNotNull(entry);
        Assert.AreEqual("files", entry.ModuleId);
        Assert.AreEqual("entity-1", entry.EntityId);
        Assert.AreEqual("Test File", entry.Title);
        Assert.AreEqual(_userId, entry.OwnerId);
    }

    [TestMethod]
    public async Task IndexDocumentAsync_ExistingDocument_UpdatesEntry()
    {
        var doc1 = CreateDocument("files", "entity-1", "FileNode", "Original Title");
        await _provider.IndexDocumentAsync(doc1);

        var doc2 = CreateDocument("files", "entity-1", "FileNode", "Updated Title");
        await _provider.IndexDocumentAsync(doc2);

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(1, count);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.AreEqual("Updated Title", entry.Title);
    }

    [TestMethod]
    public async Task IndexDocumentAsync_WithMetadata_SerializesJson()
    {
        var doc = new SearchDocument
        {
            ModuleId = "files",
            EntityId = "entity-1",
            EntityType = "FileNode",
            Title = "Test",
            Content = "content",
            OwnerId = _userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string> { ["tag"] = "important", ["mimeType"] = "text/plain" }
        };

        await _provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsNotNull(entry.MetadataJson);
        Assert.IsTrue(entry.MetadataJson.Contains("important"));
    }

    [TestMethod]
    public async Task IndexDocumentAsync_EmptyMetadata_NullJson()
    {
        var doc = CreateDocument("files", "entity-1", "FileNode", "Test");

        await _provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsNull(entry.MetadataJson);
    }

    [TestMethod]
    public async Task IndexDocumentAsync_NullDocument_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _provider.IndexDocumentAsync(null!));
    }

    [TestMethod]
    public async Task IndexDocumentAsync_SetsIndexedAt()
    {
        var before = DateTimeOffset.UtcNow;
        var doc = CreateDocument("files", "entity-1", "FileNode", "Test");
        await _provider.IndexDocumentAsync(doc);
        var after = DateTimeOffset.UtcNow;

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsTrue(entry.IndexedAt >= before && entry.IndexedAt <= after);
    }

    #endregion

    #region RemoveDocumentAsync

    [TestMethod]
    public async Task RemoveDocumentAsync_ExistingDocument_RemovesEntry()
    {
        var doc = CreateDocument("files", "entity-1", "FileNode", "Test");
        await _provider.IndexDocumentAsync(doc);

        await _provider.RemoveDocumentAsync("files", "entity-1");

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task RemoveDocumentAsync_NonExistentDocument_NoError()
    {
        await _provider.RemoveDocumentAsync("files", "nonexistent");

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    #endregion

    #region SearchAsync

    [TestMethod]
    public async Task SearchAsync_MatchingQuery_ReturnsResults()
    {
        await SeedDocuments();

        var query = new SearchQuery
        {
            QueryText = "important",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.IsTrue(result.TotalCount > 0);
        Assert.IsTrue(result.Items.Count > 0);
        Assert.IsTrue(result.Items.All(i => i.Title.Contains("important", StringComparison.OrdinalIgnoreCase) ||
                                             i.Snippet.Contains("important", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SearchAsync_NoMatch_ReturnsEmptyResults()
    {
        await SeedDocuments();

        var query = new SearchQuery
        {
            QueryText = "zzzznonexistent",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public async Task SearchAsync_ModuleFilter_ReturnsOnlyMatchingModule()
    {
        await SeedDocuments();

        var query = new SearchQuery
        {
            QueryText = "content",
            UserId = _userId,
            ModuleFilter = "notes",
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.ModuleId == "notes"));
    }

    [TestMethod]
    public async Task SearchAsync_EntityTypeFilter_ReturnsOnlyMatchingType()
    {
        await SeedDocuments();

        var query = new SearchQuery
        {
            QueryText = "content",
            UserId = _userId,
            EntityTypeFilter = "Note",
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.EntityType == "Note"));
    }

    [TestMethod]
    public async Task SearchAsync_PermissionScoped_ReturnsOnlyOwnedDocuments()
    {
        var otherUserId = Guid.NewGuid();

        // Index docs for current user
        await _provider.IndexDocumentAsync(CreateDocument("files", "e1", "FileNode", "My File"));

        // Index docs for another user
        await _provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files",
            EntityId = "e2",
            EntityType = "FileNode",
            Title = "Other User File",
            Content = "some content",
            OwnerId = otherUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var query = new SearchQuery
        {
            QueryText = "File",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("e1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task SearchAsync_Pagination_ReturnsCorrectPage()
    {
        // Seed 5 documents
        for (int i = 1; i <= 5; i++)
        {
            await _provider.IndexDocumentAsync(
                CreateDocument("files", $"e{i}", "FileNode", $"Document {i} with content"));
        }

        var query = new SearchQuery
        {
            QueryText = "Document",
            UserId = _userId,
            Page = 2,
            PageSize = 2
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(5, result.TotalCount);
        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual(2, result.Page);
        Assert.AreEqual(2, result.PageSize);
    }

    [TestMethod]
    public async Task SearchAsync_SortDateDesc_ReturnsMostRecentFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await _provider.IndexDocumentAsync(CreateDocument("files", "old", "FileNode", "Old content", now.AddHours(-2)));
        await _provider.IndexDocumentAsync(CreateDocument("files", "new", "FileNode", "New content", now));

        var query = new SearchQuery
        {
            QueryText = "content",
            UserId = _userId,
            Page = 1,
            PageSize = 20,
            SortOrder = SearchSortOrder.DateDesc
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual("new", result.Items[0].EntityId);
        Assert.AreEqual("old", result.Items[1].EntityId);
    }

    [TestMethod]
    public async Task SearchAsync_SortDateAsc_ReturnsOldestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await _provider.IndexDocumentAsync(CreateDocument("files", "old", "FileNode", "Old content", now.AddHours(-2)));
        await _provider.IndexDocumentAsync(CreateDocument("files", "new", "FileNode", "New content", now));

        var query = new SearchQuery
        {
            QueryText = "content",
            UserId = _userId,
            Page = 1,
            PageSize = 20,
            SortOrder = SearchSortOrder.DateAsc
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual("old", result.Items[0].EntityId);
        Assert.AreEqual("new", result.Items[1].EntityId);
    }

    [TestMethod]
    public async Task SearchAsync_FacetCounts_ReturnsPerModuleCounts()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "search content"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "search content"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "search content"));

        var query = new SearchQuery
        {
            QueryText = "search",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(2, result.FacetCounts["files"]);
        Assert.AreEqual(1, result.FacetCounts["notes"]);
    }

    [TestMethod]
    public async Task SearchAsync_NullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _provider.SearchAsync(null!));
    }

    [TestMethod]
    public async Task SearchAsync_WithMetadata_ReturnsDeserializedMetadata()
    {
        var doc = new SearchDocument
        {
            ModuleId = "files",
            EntityId = "e1",
            EntityType = "FileNode",
            Title = "Test File",
            Content = "searchable content",
            OwnerId = _userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string> { ["mimeType"] = "text/plain" }
        };
        await _provider.IndexDocumentAsync(doc);

        var query = new SearchQuery { QueryText = "searchable", UserId = _userId, Page = 1, PageSize = 20 };
        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("text/plain", result.Items[0].Metadata["mimeType"]);
    }

    #endregion

    #region ReindexModuleAsync

    [TestMethod]
    public async Task ReindexModuleAsync_ClearsModuleEntries()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Test"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Test"));

        await _provider.ReindexModuleAsync("files");

        var remaining = await _db.SearchIndexEntries.ToListAsync();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("notes", remaining[0].ModuleId);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_NoEntries_NoError()
    {
        await _provider.ReindexModuleAsync("nonexistent");

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    #endregion

    #region GetIndexStatsAsync

    [TestMethod]
    public async Task GetIndexStatsAsync_ReturnsCorrectStats()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Test"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Test"));

        var stats = await _provider.GetIndexStatsAsync();

        Assert.AreEqual(3, stats.TotalDocuments);
        Assert.AreEqual(2, stats.DocumentsPerModule["files"]);
        Assert.AreEqual(1, stats.DocumentsPerModule["notes"]);
        Assert.IsNotNull(stats.LastIncrementalIndexAt);
    }

    [TestMethod]
    public async Task GetIndexStatsAsync_EmptyIndex_ReturnsZeros()
    {
        var stats = await _provider.GetIndexStatsAsync();

        Assert.AreEqual(0, stats.TotalDocuments);
        Assert.AreEqual(0, stats.DocumentsPerModule.Count);
        Assert.IsNull(stats.LastIncrementalIndexAt);
        Assert.IsNull(stats.LastFullReindexAt);
    }

    [TestMethod]
    public async Task GetIndexStatsAsync_WithCompletedReindex_ReturnsLastReindexTime()
    {
        var completedTime = DateTimeOffset.UtcNow.AddHours(-1);
        _db.IndexingJobs.Add(new IndexingJob
        {
            Type = IndexJobType.Full,
            Status = IndexJobStatus.Completed,
            CompletedAt = completedTime
        });
        await _db.SaveChangesAsync();

        var stats = await _provider.GetIndexStatsAsync();

        Assert.IsNotNull(stats.LastFullReindexAt);
        Assert.AreEqual(completedTime, stats.LastFullReindexAt);
    }

    #endregion

    #region Helpers

    private SearchDocument CreateDocument(string moduleId, string entityId, string entityType, string title,
        DateTimeOffset? updatedAt = null)
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = entityType,
            Title = title,
            Content = $"This is the content for {title}",
            OwnerId = _userId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    private async Task SeedDocuments()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "An important file"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Another file with content"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Important note with content"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n2", "Note", "Quick note"));
        await _provider.IndexDocumentAsync(CreateDocument("chat", "m1", "Message", "Chat message content"));
    }

    #endregion
}
