using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Events;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// End-to-end Phase 4 integration tests verifying the complete indexing pipeline:
/// event handler → indexing service → search provider → query service.
/// </summary>
[TestClass]
public class IndexingPipelineIntegrationTests
{
    private SearchDbContext _db = null!;
    private MariaDbSearchProvider _provider = null!;
    private SearchIndexingService _indexingService = null!;
    private SearchQueryService _queryService = null!;
    private SearchIndexRequestEventHandler _eventHandler = null!;
    private Mock<ISearchableModule> _filesModuleMock = null!;
    private Mock<ISearchableModule> _notesModuleMock = null!;
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

        _filesModuleMock = new Mock<ISearchableModule>();
        _filesModuleMock.Setup(m => m.ModuleId).Returns("files");
        _filesModuleMock.Setup(m => m.SupportedEntityTypes)
            .Returns(new[] { "FileNode", "Folder" });
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDoc("files", "f1", "FileNode", "Project Plan", "Detailed project plan for Q3"),
                CreateDoc("files", "f2", "FileNode", "Budget Report", "Annual budget analysis and projections"),
                CreateDoc("files", "f3", "Folder", "Projects", "")
            });

        _notesModuleMock = new Mock<ISearchableModule>();
        _notesModuleMock.Setup(m => m.ModuleId).Returns("notes");
        _notesModuleMock.Setup(m => m.SupportedEntityTypes)
            .Returns(new[] { "Note" });
        _notesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDoc("notes", "n1", "Note", "Meeting Minutes", "Meeting about project plan timeline"),
                CreateDoc("notes", "n2", "Note", "Personal Notes", "Shopping list and reminders")
            });

        var extractionService = new ContentExtractionService(
            [],
            NullLogger<ContentExtractionService>.Instance);

        _indexingService = new SearchIndexingService(
            _provider,
            [_filesModuleMock.Object, _notesModuleMock.Object],
            extractionService,
            NullLogger<SearchIndexingService>.Instance);

        _queryService = new SearchQueryService(
            _provider,
            NullLogger<SearchQueryService>.Instance);

        _eventHandler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _provider,
            _indexingService);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _indexingService.Dispose();
        _db.Dispose();
    }

    // --- Full Pipeline E2E Tests ---

    [TestMethod]
    public async Task FullPipeline_EventToSearchResult()
    {
        // 1. Module provides document
        var doc = CreateDoc("files", "new-file", "FileNode", "Quarterly Report",
            "Q3 revenue analysis and trends");
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("new-file", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        // 2. Event handler receives Index event → enqueues to indexing service
        _indexingService.Start();

        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "new-file",
            Action = SearchIndexAction.Index
        });

        // 3. Wait for processing
        await Task.Delay(500);
        await _indexingService.StopAsync();

        // 4. Verify document is in index
        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(1, count);

        // 5. Search and find the document
        var result = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "Quarterly",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
        // Phase 5 title highlighting wraps matched terms in <mark> tags
        Assert.IsTrue(result.Items[0].Title.Contains("Quarterly", StringComparison.OrdinalIgnoreCase),
            $"Title should contain the search term, got: {result.Items[0].Title}");
    }

    [TestMethod]
    public async Task FullPipeline_RemoveEvent_DocumentDisappears()
    {
        // 1. Index a document first
        await _provider.IndexDocumentAsync(
            CreateDoc("files", "to-delete", "FileNode", "Temporary File", "This will be removed"));

        // 2. Verify it's searchable
        var beforeResult = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "Temporary",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });
        Assert.AreEqual(1, beforeResult.TotalCount);

        // 3. Fire Remove event
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "to-delete",
            Action = SearchIndexAction.Remove
        });

        // 4. Verify document is gone
        var afterResult = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "Temporary",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });
        Assert.AreEqual(0, afterResult.TotalCount);
    }

    [TestMethod]
    public async Task FullPipeline_MultiModuleIndex_CrossModuleSearch()
    {
        // Index documents from multiple modules
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
                CreateDoc("files", id, "FileNode", $"File {id}", "project plan content"));

        _notesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) =>
                CreateDoc("notes", id, "Note", $"Note {id}", "project plan notes"));

        _indexingService.Start();

        // Index from files module
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "files", EntityId = "f1", Action = SearchIndexAction.Index
        });

        // Index from notes module
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "notes", EntityId = "n1", Action = SearchIndexAction.Index
        });

        await Task.Delay(500);
        await _indexingService.StopAsync();

        // Cross-module search
        var result = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "project plan",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(2, result.TotalCount);

        // Verify facet counts
        Assert.AreEqual(1, result.FacetCounts["files"]);
        Assert.AreEqual(1, result.FacetCounts["notes"]);
    }

    [TestMethod]
    public async Task FullPipeline_UpdateExistingDocument_ReflectsChanges()
    {
        // 1. Index original
        var original = CreateDoc("notes", "n1", "Note", "Draft", "Initial draft content");
        _notesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);

        _indexingService.Start();
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "notes", EntityId = "n1", Action = SearchIndexAction.Index
        });
        await Task.Delay(300);

        // 2. Update the document
        var updated = CreateDoc("notes", "n1", "Note", "Final Version", "Completed final content with revisions");
        _notesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "notes", EntityId = "n1", Action = SearchIndexAction.Index
        });
        await Task.Delay(300);
        await _indexingService.StopAsync();

        // 3. Verify updated content is searchable
        var result = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "Final Version",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
        // Phase 5 title highlighting wraps matched terms in <mark> tags
        Assert.IsTrue(result.Items[0].Title.Contains("Final", StringComparison.OrdinalIgnoreCase),
            $"Title should contain search terms, got: {result.Items[0].Title}");

        // 4. Old title should not match
        var oldResult = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "Draft",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });
        Assert.AreEqual(0, oldResult.TotalCount);
    }

    [TestMethod]
    public async Task FullPipeline_EntityDeletedBetweenEventAndProcessing_CleanedUp()
    {
        // Pre-index a document
        await _provider.IndexDocumentAsync(
            CreateDoc("files", "deleted", "FileNode", "Deleted File", "content"));

        // Module says the entity no longer exists
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("deleted", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchDocument?)null);

        _indexingService.Start();

        // Send Index event for deleted entity
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "files", EntityId = "deleted", Action = SearchIndexAction.Index
        });

        await Task.Delay(300);
        await _indexingService.StopAsync();

        // Document should be removed from index
        var count = await _db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task FullPipeline_StatsReflectIndexedDocuments()
    {
        // Index several documents
        await _provider.IndexDocumentAsync(CreateDoc("files", "f1", "FileNode", "File 1", "c"));
        await _provider.IndexDocumentAsync(CreateDoc("files", "f2", "FileNode", "File 2", "c"));
        await _provider.IndexDocumentAsync(CreateDoc("notes", "n1", "Note", "Note 1", "c"));

        var stats = await _queryService.GetStatsAsync();

        Assert.AreEqual(3, stats.TotalDocuments);
        Assert.AreEqual(2, stats.DocumentsPerModule["files"]);
        Assert.AreEqual(1, stats.DocumentsPerModule["notes"]);
    }

    [TestMethod]
    public async Task FullPipeline_ReindexModule_RefreshesIndex()
    {
        // Index stale data
        await _provider.IndexDocumentAsync(CreateDoc("files", "stale1", "FileNode", "Stale", "old"));
        await _provider.IndexDocumentAsync(CreateDoc("files", "stale2", "FileNode", "Stale", "old"));

        // Reindex clears the module
        await _queryService.ReindexModuleAsync("files");

        var stats = await _queryService.GetStatsAsync();
        Assert.AreEqual(0, stats.TotalDocuments);
    }

    [TestMethod]
    public async Task FullPipeline_EmptyQueryText_ReturnsEmptyResults()
    {
        await _provider.IndexDocumentAsync(CreateDoc("files", "f1", "FileNode", "Test", "content"));

        var result = await _queryService.SearchAsync(new SearchQuery
        {
            QueryText = "",
            UserId = _userId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(0, result.TotalCount);
    }

    [TestMethod]
    public async Task FullPipeline_ProcessingCounters_TrackActivity()
    {
        var doc = CreateDoc("files", "f1", "FileNode", "Test", "content");
        _filesModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("f1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        _indexingService.Start();

        // Three successful operations
        await _eventHandler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
            ModuleId = "files", EntityId = "f1", Action = SearchIndexAction.Index
        });

        await Task.Delay(300);
        await _indexingService.StopAsync();

        Assert.IsTrue(_indexingService.TotalProcessed > 0);
        Assert.AreEqual(0L, _indexingService.TotalFailed);
    }

    // --- Helpers ---

    private SearchDocument CreateDoc(
        string moduleId, string entityId, string entityType,
        string title, string content) => new()
    {
        ModuleId = moduleId,
        EntityId = entityId,
        EntityType = entityType,
        Title = title,
        Content = content,
        OwnerId = _userId,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
