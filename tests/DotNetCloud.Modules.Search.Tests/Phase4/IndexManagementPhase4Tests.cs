using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// Phase 4 tests for index management — statistics, stale cleanup,
/// and provider-agnostic indexing operations using InMemory database.
/// </summary>
[TestClass]
public class IndexManagementPhase4Tests
{
    private SearchDbContext _db = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SearchDbContext(options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // --- Index Statistics Tests (via MariaDbSearchProvider as reference implementation) ---

    [TestMethod]
    public async Task GetIndexStats_EmptyIndex_ReturnsZeroCounts()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var stats = await provider.GetIndexStatsAsync();

        Assert.AreEqual(0, stats.TotalDocuments);
        Assert.AreEqual(0, stats.DocumentsPerModule.Count);
        Assert.IsNull(stats.LastFullReindexAt);
        Assert.IsNull(stats.LastIncrementalIndexAt);
    }

    [TestMethod]
    public async Task GetIndexStats_WithDocuments_ReturnsCorrectCounts()
    {
        // Seed data
        await SeedIndexEntries("files", 5);
        await SeedIndexEntries("notes", 3);
        await SeedIndexEntries("chat", 2);

        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var stats = await provider.GetIndexStatsAsync();

        Assert.AreEqual(10, stats.TotalDocuments);
        Assert.AreEqual(3, stats.DocumentsPerModule.Count);
        Assert.AreEqual(5, stats.DocumentsPerModule["files"]);
        Assert.AreEqual(3, stats.DocumentsPerModule["notes"]);
        Assert.AreEqual(2, stats.DocumentsPerModule["chat"]);
    }

    [TestMethod]
    public async Task GetIndexStats_LastIncrementalIndexAt_ReturnsLatestIndexedAt()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = DateTimeOffset.UtcNow;

        _db.SearchIndexEntries.Add(CreateEntry("files", "e1", older));
        _db.SearchIndexEntries.Add(CreateEntry("files", "e2", newer));
        await _db.SaveChangesAsync();

        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var stats = await provider.GetIndexStatsAsync();

        Assert.IsNotNull(stats.LastIncrementalIndexAt);
        Assert.AreEqual(newer.UtcDateTime.Date, stats.LastIncrementalIndexAt.Value.UtcDateTime.Date);
    }

    [TestMethod]
    public async Task GetIndexStats_CompletedFullReindex_ReturnsLastReindexTime()
    {
        var completedAt = DateTimeOffset.UtcNow.AddHours(-1);

        _db.IndexingJobs.Add(new IndexingJob
        {
            Type = IndexJobType.Full,
            Status = IndexJobStatus.Completed,
            CompletedAt = completedAt,
            DocumentsProcessed = 50,
            DocumentsTotal = 50
        });
        await _db.SaveChangesAsync();

        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var stats = await provider.GetIndexStatsAsync();

        Assert.IsNotNull(stats.LastFullReindexAt);
    }

    // --- Index/Remove/Reindex Operations ---

    [TestMethod]
    public async Task IndexDocument_NewDocument_AddsToIndex()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var doc = CreateDocument("files", "e1", "FileNode", "Test File", "File content");
        await provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.AreEqual("files", entry.ModuleId);
        Assert.AreEqual("e1", entry.EntityId);
        Assert.AreEqual("Test File", entry.Title);
        Assert.AreEqual("File content", entry.Content);
    }

    [TestMethod]
    public async Task IndexDocument_ExistingDocument_Updates()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var original = CreateDocument("files", "e1", "FileNode", "Original", "Original content");
        await provider.IndexDocumentAsync(original);

        var updated = CreateDocument("files", "e1", "FileNode", "Updated Title", "Updated content");
        await provider.IndexDocumentAsync(updated);

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(1, count);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.AreEqual("Updated Title", entry.Title);
        Assert.AreEqual("Updated content", entry.Content);
    }

    [TestMethod]
    public async Task IndexDocument_SetsIndexedAt()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var before = DateTimeOffset.UtcNow;

        var doc = CreateDocument("files", "e1", "FileNode", "Title", "Content");
        await provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsTrue(entry.IndexedAt >= before);
    }

    [TestMethod]
    public async Task IndexDocument_WithMetadata_SerializedToJson()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var doc = new SearchDocument
        {
            ModuleId = "files",
            EntityId = "e1",
            EntityType = "FileNode",
            Title = "Test",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["MimeType"] = "application/pdf",
                ["path"] = "/docs/report.pdf"
            }
        };

        await provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsNotNull(entry.MetadataJson);
        Assert.IsTrue(entry.MetadataJson.Contains("application/pdf"));
        Assert.IsTrue(entry.MetadataJson.Contains("/docs/report.pdf"));
    }

    [TestMethod]
    public async Task IndexDocument_EmptyMetadata_NullMetadataJson()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var doc = CreateDocument("files", "e1", "FileNode", "Title", "Content");
        await provider.IndexDocumentAsync(doc);

        var entry = await _db.SearchIndexEntries.FirstAsync();
        Assert.IsNull(entry.MetadataJson);
    }

    [TestMethod]
    public async Task RemoveDocument_ExistingEntry_Removed()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var doc = CreateDocument("files", "e1", "FileNode", "Title", "Content");
        await provider.IndexDocumentAsync(doc);

        await provider.RemoveDocumentAsync("files", "e1");

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task RemoveDocument_NonExistentEntry_NoOp()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.RemoveDocumentAsync("files", "nonexistent");

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ReindexModule_ClearsOnlyTargetModule()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "File 1", "content"));
        await provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "File 2", "content"));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Note 1", "content"));

        await provider.ReindexModuleAsync("files");

        var remaining = await _db.SearchIndexEntries.ToListAsync();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("notes", remaining[0].ModuleId);
    }

    // --- Search Query Tests (InMemory fallback, no FTS) ---

    [TestMethod]
    public async Task Search_ByTitle_FindsMatches()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Quarterly Report", "content", userId));
        await provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Annual Budget", "content", userId));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Meeting Notes", "about Quarterly results", userId));

        var query = new SearchQuery
        {
            QueryText = "Quarterly",
            UserId = userId,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(2, result.TotalCount);
    }

    [TestMethod]
    public async Task Search_PermissionScoped_OnlyOwnDocuments()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Shared Report", "content", user1));
        await provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Shared Report", "content", user2));

        var query = new SearchQuery
        {
            QueryText = "Report",
            UserId = user1,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("f1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task Search_WithModuleFilter_RestrictsResults()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test", "content", userId));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Test", "content", userId));

        var query = new SearchQuery
        {
            QueryText = "Test",
            ModuleFilter = "files",
            UserId = userId,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("files", result.Items[0].ModuleId);
    }

    [TestMethod]
    public async Task Search_WithEntityTypeFilter_RestrictsResults()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test", "content", userId));
        await provider.IndexDocumentAsync(CreateDocument("files", "f2", "Folder", "Test", "content", userId));

        var query = new SearchQuery
        {
            QueryText = "Test",
            EntityTypeFilter = "FileNode",
            UserId = userId,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("FileNode", result.Items[0].EntityType);
    }

    [TestMethod]
    public async Task Search_Pagination_ReturnsCorrectPage()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        for (int i = 1; i <= 15; i++)
        {
            await provider.IndexDocumentAsync(
                CreateDocument("files", $"f{i}", "FileNode", $"Test File {i}", "searchable content", userId));
        }

        var page1 = new SearchQuery { QueryText = "searchable", UserId = userId, Page = 1, PageSize = 5 };
        var page2 = new SearchQuery { QueryText = "searchable", UserId = userId, Page = 2, PageSize = 5 };
        var page3 = new SearchQuery { QueryText = "searchable", UserId = userId, Page = 3, PageSize = 5 };

        var result1 = await provider.SearchAsync(page1);
        var result2 = await provider.SearchAsync(page2);
        var result3 = await provider.SearchAsync(page3);

        Assert.AreEqual(15, result1.TotalCount);
        Assert.AreEqual(5, result1.Items.Count);
        Assert.AreEqual(5, result2.Items.Count);
        Assert.AreEqual(5, result3.Items.Count);
    }

    [TestMethod]
    public async Task Search_FacetCounts_ReturnsPerModuleCounts()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test", "keyword", userId));
        await provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "Test", "keyword", userId));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Test", "keyword", userId));
        await provider.IndexDocumentAsync(CreateDocument("chat", "c1", "Message", "Test", "keyword", userId));

        var query = new SearchQuery { QueryText = "keyword", UserId = userId, Page = 1, PageSize = 20 };
        var result = await provider.SearchAsync(query);

        Assert.AreEqual(4, result.TotalCount);
        Assert.AreEqual(2, result.FacetCounts["files"]);
        Assert.AreEqual(1, result.FacetCounts["notes"]);
        Assert.AreEqual(1, result.FacetCounts["chat"]);
    }

    [TestMethod]
    public async Task Search_SortByDateDesc_OrdersCorrectly()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "old", EntityType = "FileNode",
            Title = "Old", Content = "searchable", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        });

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "new", EntityType = "FileNode",
            Title = "New", Content = "searchable", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var query = new SearchQuery
        {
            QueryText = "searchable",
            UserId = userId,
            Page = 1,
            PageSize = 20,
            SortOrder = SearchSortOrder.DateDesc
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual("new", result.Items[0].EntityId);
        Assert.AreEqual("old", result.Items[1].EntityId);
    }

    [TestMethod]
    public async Task Search_SortByDateAsc_OrdersCorrectly()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "old", EntityType = "FileNode",
            Title = "Old", Content = "searchable", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        });

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "new", EntityType = "FileNode",
            Title = "New", Content = "searchable", OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var query = new SearchQuery
        {
            QueryText = "searchable",
            UserId = userId,
            Page = 1,
            PageSize = 20,
            SortOrder = SearchSortOrder.DateAsc
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual("old", result.Items[0].EntityId);
        Assert.AreEqual("new", result.Items[1].EntityId);
    }

    [TestMethod]
    public async Task Search_SnippetGeneration_ContainsQueryText()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument(
            "files", "f1", "FileNode", "Report",
            "The quarterly earnings report shows strong growth in Q3. Revenue increased by 15% year-over-year.",
            userId));

        var query = new SearchQuery { QueryText = "quarterly", UserId = userId, Page = 1, PageSize = 20 };
        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.Items.Count);
        Assert.IsTrue(result.Items[0].Snippet.Contains("quarterly", StringComparison.OrdinalIgnoreCase));
    }

    // --- Multi-Provider Consistency Tests ---

    [TestMethod]
    public async Task SqlServerProvider_IndexAndSearch_Works()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test Document", "content here", userId));

        var query = new SearchQuery { QueryText = "Test", UserId = userId, Page = 1, PageSize = 20 };
        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task MariaDbProvider_IndexAndSearch_Works()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var userId = Guid.NewGuid();

        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test Document", "content here", userId));

        var query = new SearchQuery { QueryText = "Test", UserId = userId, Page = 1, PageSize = 20 };
        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task AllProviders_ReindexModule_ClearsEntries()
    {
        var providers = new ISearchProvider[]
        {
            new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance),
            new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance),
            new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance)
        };

        foreach (var provider in providers)
        {
            await provider.IndexDocumentAsync(CreateDocument("test", "e1", "Type", "Doc", "content"));
        }

        // Use first provider to reindex — all share the same DbContext
        await providers[0].ReindexModuleAsync("test");

        var count = await _db.SearchIndexEntries.Where(e => e.ModuleId == "test").CountAsync();
        Assert.AreEqual(0, count);
    }

    // --- Helpers ---

    private async Task SeedIndexEntries(string moduleId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _db.SearchIndexEntries.Add(CreateEntry(moduleId, $"{moduleId}-{i}", DateTimeOffset.UtcNow));
        }
        await _db.SaveChangesAsync();
    }

    private static SearchIndexEntry CreateEntry(string moduleId, string entityId, DateTimeOffset indexedAt) => new()
    {
        ModuleId = moduleId,
        EntityId = entityId,
        EntityType = "Test",
        Title = $"Test {entityId}",
        Content = "content",
        OwnerId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        IndexedAt = indexedAt
    };

    private static SearchDocument CreateDocument(
        string moduleId, string entityId, string entityType,
        string title, string content, Guid? ownerId = null) => new()
    {
        ModuleId = moduleId,
        EntityId = entityId,
        EntityType = entityType,
        Title = title,
        Content = content,
        OwnerId = ownerId ?? Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
