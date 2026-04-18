using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="MariaDbSearchProvider"/> using InMemory database.
/// The MariaDB provider falls back to Contains() which works with InMemory.
/// </summary>
[TestClass]
public class MariaDbSearchProviderTests
{
    private SearchDbContext _db = null!;
    private MariaDbSearchProvider _provider = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SearchDbContext(options);
        _provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        _userId = Guid.NewGuid();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

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
    }

    [TestMethod]
    public async Task IndexDocumentAsync_ExistingDocument_UpdatesInPlace()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "e1", "FileNode", "V1"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "e1", "FileNode", "V2"));

        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(1, count);
        Assert.AreEqual("V2", (await _db.SearchIndexEntries.FirstAsync()).Title);
    }

    [TestMethod]
    public async Task RemoveDocumentAsync_ExistingDocument_RemovesIt()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "e1", "FileNode", "Test"));
        await _provider.RemoveDocumentAsync("files", "e1");

        Assert.AreEqual(0, await _db.SearchIndexEntries.CountAsync());
    }

    [TestMethod]
    public async Task RemoveDocumentAsync_NonExistent_NoOp()
    {
        await _provider.RemoveDocumentAsync("files", "nonexistent");
        Assert.AreEqual(0, await _db.SearchIndexEntries.CountAsync());
    }

    [TestMethod]
    public async Task SearchAsync_QueryMatchesTitle_ReturnsResults()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "e1", "FileNode", "Project Roadmap"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "e2", "FileNode", "Meeting Notes"));

        var result = await _provider.SearchAsync(new SearchQuery
        {
            QueryText = "Roadmap",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("e1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task SearchAsync_QueryMatchesContent_ReturnsResults()
    {
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "My Note"));

        var result = await _provider.SearchAsync(new SearchQuery
        {
            QueryText = "content for My Note",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task SearchAsync_PermissionScoped_IsolatesUsers()
    {
        var otherUser = Guid.NewGuid();
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Shared keyword file"));
        await _provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "f2", EntityType = "FileNode",
            Title = "Other user keyword file", Content = "Other content",
            OwnerId = otherUser, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await _provider.SearchAsync(new SearchQuery
        {
            QueryText = "keyword",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("f1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task SearchAsync_FacetCounts_AreAccurate()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Search term doc1"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Search term doc2"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n2", "Note", "Search term doc3"));

        var result = await _provider.SearchAsync(new SearchQuery
        {
            QueryText = "Search term",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.FacetCounts["files"]);
        Assert.AreEqual(2, result.FacetCounts["notes"]);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_ClearsOnlyTargetModule()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "Test"));
        await _provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "Test"));

        await _provider.ReindexModuleAsync("files");

        var remaining = await _db.SearchIndexEntries.ToListAsync();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("notes", remaining[0].ModuleId);
    }

    [TestMethod]
    public async Task GetIndexStatsAsync_ReturnsCorrectCounts()
    {
        await _provider.IndexDocumentAsync(CreateDocument("files", "f1", "FileNode", "T1"));
        await _provider.IndexDocumentAsync(CreateDocument("files", "f2", "FileNode", "T2"));

        var stats = await _provider.GetIndexStatsAsync();

        Assert.AreEqual(2, stats.TotalDocuments);
        Assert.AreEqual(2, stats.DocumentsPerModule["files"]);
    }

    private SearchDocument CreateDocument(string moduleId, string entityId, string entityType, string title)
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
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
