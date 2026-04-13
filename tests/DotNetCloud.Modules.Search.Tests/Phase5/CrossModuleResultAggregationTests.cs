using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests.Phase5;

/// <summary>
/// Tests for cross-module result aggregation — facet counting, multi-module search,
/// pagination across modules, and result merging.
/// </summary>
[TestClass]
public class CrossModuleResultAggregationTests
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

        SeedMultiModuleData();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private void SeedMultiModuleData()
    {
        var now = DateTimeOffset.UtcNow;

        // 5 files entries
        for (int i = 1; i <= 5; i++)
        {
            _db.SearchIndexEntries.Add(new SearchIndexEntry
            {
                ModuleId = "files", EntityId = $"file-{i}", EntityType = "FileNode",
                Title = $"Project Document {i}", Content = $"This is project document number {i} with important content.",
                OwnerId = _userId, CreatedAt = now.AddDays(-i), UpdatedAt = now.AddDays(-i), IndexedAt = now
            });
        }

        // 3 notes entries
        for (int i = 1; i <= 3; i++)
        {
            _db.SearchIndexEntries.Add(new SearchIndexEntry
            {
                ModuleId = "notes", EntityId = $"note-{i}", EntityType = "Note",
                Title = $"Project Note {i}", Content = $"Project planning note {i} with details about the project.",
                OwnerId = _userId, CreatedAt = now.AddDays(-i), UpdatedAt = now.AddDays(-i), IndexedAt = now
            });
        }

        // 2 chat entries
        for (int i = 1; i <= 2; i++)
        {
            _db.SearchIndexEntries.Add(new SearchIndexEntry
            {
                ModuleId = "chat", EntityId = $"msg-{i}", EntityType = "Message",
                Title = $"Project Channel", Content = $"Discussion about the project in message {i}.",
                OwnerId = _userId, CreatedAt = now.AddDays(-i), UpdatedAt = now.AddDays(-i), IndexedAt = now
            });
        }

        // 4 calendar entries
        for (int i = 1; i <= 4; i++)
        {
            _db.SearchIndexEntries.Add(new SearchIndexEntry
            {
                ModuleId = "calendar", EntityId = $"event-{i}", EntityType = "CalendarEvent",
                Title = $"Project Meeting {i}", Content = $"Scheduled project meeting {i} to discuss milestones.",
                OwnerId = _userId, CreatedAt = now.AddDays(-i), UpdatedAt = now.AddDays(-i), IndexedAt = now
            });
        }

        // 1 contacts entry
        _db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "contacts", EntityId = "contact-1", EntityType = "Contact",
            Title = "Project Lead", Content = "John Doe, project lead at DotNetCloud.",
            OwnerId = _userId, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1), IndexedAt = now
        });

        _db.SaveChanges();
    }

    #region Facet Count Tests

    [TestMethod]
    public async Task FacetCounts_MultiModuleSearch_ShowsAllModules()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId };

        var result = await _provider.SearchAsync(query);

        Assert.IsTrue(result.FacetCounts.Count >= 4, $"Expected at least 4 module facets, got {result.FacetCounts.Count}");
        Assert.IsTrue(result.FacetCounts.ContainsKey("files"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("notes"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("chat"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("calendar"));
    }

    [TestMethod]
    public async Task FacetCounts_CorrectCountsPerModule()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(5, result.FacetCounts["files"]);
        Assert.AreEqual(3, result.FacetCounts["notes"]);
        Assert.AreEqual(2, result.FacetCounts["chat"]);
        Assert.AreEqual(4, result.FacetCounts["calendar"]);
        Assert.AreEqual(1, result.FacetCounts["contacts"]);
    }

    [TestMethod]
    public async Task FacetCounts_WithModuleFilter_StillShowsAllModuleFacets()
    {
        // When searching with a module filter, facets still show counts for ALL matching modules
        var query = new SearchQuery { QueryText = "project", UserId = _userId, ModuleFilter = "files" };

        var result = await _provider.SearchAsync(query);

        // Items should be filtered to files only
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "files"));
        // But facet counts show all modules (they compute without the module filter)
        Assert.IsTrue(result.FacetCounts.Count >= 4);
    }

    [TestMethod]
    public async Task TotalCount_ReflectsAllMatchingDocuments()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(15, result.TotalCount, "Should count all 15 seeded project-related documents");
    }

    #endregion

    #region Pagination Tests

    [TestMethod]
    public async Task Pagination_FirstPage_ReturnsCorrectSubset()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, Page = 1, PageSize = 5 };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(5, result.PageSize);
        Assert.AreEqual(5, result.Items.Count);
        Assert.AreEqual(15, result.TotalCount);
    }

    [TestMethod]
    public async Task Pagination_SecondPage_ReturnsDifferentSubset()
    {
        var query1 = new SearchQuery { QueryText = "project", UserId = _userId, Page = 1, PageSize = 5, SortOrder = SearchSortOrder.DateDesc };
        var query2 = new SearchQuery { QueryText = "project", UserId = _userId, Page = 2, PageSize = 5, SortOrder = SearchSortOrder.DateDesc };

        var result1 = await _provider.SearchAsync(query1);
        var result2 = await _provider.SearchAsync(query2);

        Assert.AreEqual(5, result1.Items.Count);
        Assert.AreEqual(5, result2.Items.Count);

        var ids1 = result1.Items.Select(i => i.EntityId).ToHashSet();
        var ids2 = result2.Items.Select(i => i.EntityId).ToHashSet();
        Assert.IsFalse(ids1.Overlaps(ids2), "Pages should not overlap");
    }

    [TestMethod]
    public async Task Pagination_LastPage_ReturnsRemainingItems()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, Page = 3, PageSize = 5 };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(3, result.Page);
        Assert.AreEqual(5, result.Items.Count); // 15 total, page 3 of 5 = 5 items
    }

    [TestMethod]
    public async Task Pagination_PageBeyondResults_ReturnsEmpty()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, Page = 10, PageSize = 5 };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(15, result.TotalCount); // Total still accurate
    }

    [TestMethod]
    public async Task Pagination_SingleItemPerPage_WorksCorrectly()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, Page = 1, PageSize = 1 };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual(15, result.TotalCount);
    }

    #endregion

    #region Module Filter Tests

    [TestMethod]
    public async Task ModuleFilter_FilesOnly_ReturnsOnlyFiles()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, ModuleFilter = "files" };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(5, result.Items.Count);
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "files"));
    }

    [TestMethod]
    public async Task ModuleFilter_NotesOnly_ReturnsOnlyNotes()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, ModuleFilter = "notes" };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(3, result.Items.Count);
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "notes"));
    }

    [TestMethod]
    public async Task ModuleFilter_NonexistentModule_ReturnsEmpty()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, ModuleFilter = "nonexistent" };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Items.Count);
    }

    #endregion

    #region Entity Type Filter Tests

    [TestMethod]
    public async Task EntityTypeFilter_NoteOnly_ReturnsOnlyNotes()
    {
        var query = new SearchQuery
        {
            QueryText = "project",
            UserId = _userId,
            EntityTypeFilter = "Note"
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(3, result.Items.Count);
        Assert.IsTrue(result.Items.All(i => i.EntityType == "Note"));
    }

    [TestMethod]
    public async Task EntityTypeFilter_CalendarEvent_ReturnsOnlyEvents()
    {
        var query = new SearchQuery
        {
            QueryText = "project",
            UserId = _userId,
            EntityTypeFilter = "CalendarEvent"
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(4, result.Items.Count);
        Assert.IsTrue(result.Items.All(i => i.EntityType == "CalendarEvent"));
    }

    [TestMethod]
    public async Task CombinedModuleAndTypeFilter_NarrowsResults()
    {
        var query = new SearchQuery
        {
            QueryText = "project",
            UserId = _userId,
            ModuleFilter = "files",
            EntityTypeFilter = "FileNode"
        };

        var result = await _provider.SearchAsync(query);

        Assert.AreEqual(5, result.Items.Count);
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "files" && i.EntityType == "FileNode"));
    }

    #endregion

    #region Sort Order Tests

    [TestMethod]
    public async Task SortByDateDesc_MostRecentFirst()
    {
        var query = new SearchQuery
        {
            QueryText = "project",
            UserId = _userId,
            SortOrder = SearchSortOrder.DateDesc,
            PageSize = 50
        };

        var result = await _provider.SearchAsync(query);

        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            Assert.IsTrue(result.Items[i].UpdatedAt >= result.Items[i + 1].UpdatedAt,
                $"Item {i} ({result.Items[i].UpdatedAt}) should be >= item {i + 1} ({result.Items[i + 1].UpdatedAt})");
        }
    }

    [TestMethod]
    public async Task SortByDateAsc_OldestFirst()
    {
        var query = new SearchQuery
        {
            QueryText = "project",
            UserId = _userId,
            SortOrder = SearchSortOrder.DateAsc,
            PageSize = 50
        };

        var result = await _provider.SearchAsync(query);

        for (int i = 0; i < result.Items.Count - 1; i++)
        {
            Assert.IsTrue(result.Items[i].UpdatedAt <= result.Items[i + 1].UpdatedAt,
                $"Item {i} ({result.Items[i].UpdatedAt}) should be <= item {i + 1} ({result.Items[i + 1].UpdatedAt})");
        }
    }

    #endregion

    #region Metadata Tests

    [TestMethod]
    public async Task Metadata_WhenPresent_DeserializedCorrectly()
    {
        // Add an entry with metadata
        _db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "files", EntityId = "meta-test", EntityType = "FileNode",
            Title = "Metadata Test File", Content = "Project file with metadata",
            OwnerId = _userId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            IndexedAt = DateTimeOffset.UtcNow,
            MetadataJson = "{\"MimeType\":\"application/pdf\",\"FileSize\":\"1024\"}"
        });
        await _db.SaveChangesAsync();

        var query = new SearchQuery { QueryText = "Metadata Test", UserId = _userId };

        var result = await _provider.SearchAsync(query);

        var item = result.Items.FirstOrDefault(i => i.EntityId == "meta-test");
        Assert.IsNotNull(item);
        Assert.AreEqual("application/pdf", item.Metadata["MimeType"]);
        Assert.AreEqual("1024", item.Metadata["FileSize"]);
    }

    [TestMethod]
    public async Task Metadata_WhenNull_ReturnsEmptyDictionary()
    {
        var query = new SearchQuery { QueryText = "project", UserId = _userId, PageSize = 1 };

        var result = await _provider.SearchAsync(query);

        Assert.IsTrue(result.Items.Count > 0);
        Assert.IsNotNull(result.Items[0].Metadata);
    }

    #endregion
}
