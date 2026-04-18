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

namespace DotNetCloud.Modules.Search.Tests.Phase8;

/// <summary>
/// End-to-end integration tests that verify the full indexing pipeline:
/// Entity creation → SearchIndexRequestEvent → EventHandler → IndexingService → SearchProvider → Query returns results.
/// </summary>
[TestClass]
public class EndToEndIndexingTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private SearchDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SearchDbContext(options);
    }

    private static SearchDocument CreateDocument(string moduleId, string entityId, string title, string content)
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = "TestEntity",
            Title = title,
            Content = content,
            OwnerId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        };
    }

    /// <summary>Creates a SearchIndexingService with proper DI scope factory.</summary>
    private static (SearchIndexingService service, ServiceProvider sp) CreateIndexingService(
        ISearchProvider provider,
        IContentExtractor[]? extractors = null,
        params ISearchableModule[] modules)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISearchProvider>(_ => provider);
        foreach (var m in modules)
            services.AddScoped<ISearchableModule>(_ => m);
        services.AddScoped<ContentExtractionService>();
        services.AddSingleton<IEnumerable<IContentExtractor>>(extractors ?? []);
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var svc = new SearchIndexingService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

        return (svc, sp);
    }

    [TestMethod]
    public async Task IndexEvent_ThroughPipeline_DocumentAppersInSearch()
    {
        using var db = CreateDbContext(nameof(IndexEvent_ThroughPipeline_DocumentAppersInSearch));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var module = new Mock<ISearchableModule>();
        module.Setup(m => m.ModuleId).Returns("notes");
        module.Setup(m => m.GetSearchableDocumentAsync("note-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument("notes", "note-1", "meeting notes", "discuss quarterly budget and projections"));

        var (indexingService, sp) = CreateIndexingService(provider, null, module.Object);
        using var _sp = sp;

        var handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance, provider, indexingService);

        indexingService.Start();
        try
        {
            // Simulate event from NoteService creating a note
            var indexEvent = new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "notes",
                EntityId = "note-1",
                Action = SearchIndexAction.Index
            };

            await handler.HandleAsync(indexEvent);

            // Allow background processing to complete
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100);
                if (indexingService.TotalProcessed > 0) break;
            }

            // Now search for the document
            var query = new SearchQuery
            {
                QueryText = "quarterly budget",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            };

            var result = await provider.SearchAsync(query);

            Assert.IsTrue(result.TotalCount >= 1, "Document should appear in search results");
            Assert.IsTrue(result.Items.Any(i => i.EntityId == "note-1"));
        }
        finally
        {
            await indexingService.StopAsync();
        }
    }

    [TestMethod]
    public async Task RemoveEvent_ThroughPipeline_DocumentDisappearsFromSearch()
    {
        using var db = CreateDbContext(nameof(RemoveEvent_ThroughPipeline_DocumentDisappearsFromSearch));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Pre-index a document
        await provider.IndexDocumentAsync(CreateDocument("notes", "note-2", "Old Note", "to be deleted"));

        var handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance, provider);

        // Simulate delete event
        var removeEvent = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "notes",
            EntityId = "note-2",
            Action = SearchIndexAction.Remove
        };

        await handler.HandleAsync(removeEvent);

        // Verify removed from search
        var query = new SearchQuery
        {
            QueryText = "deleted",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);
        Assert.AreEqual(0, result.TotalCount);
    }

    [TestMethod]
    public async Task UpdateEvent_ThroughPipeline_DocumentIsUpdated()
    {
        using var db = CreateDbContext(nameof(UpdateEvent_ThroughPipeline_DocumentIsUpdated));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Pre-index original document
        await provider.IndexDocumentAsync(CreateDocument("notes", "note-3", "original title", "original content"));

        // Module returns updated version
        var module = new Mock<ISearchableModule>();
        module.Setup(m => m.ModuleId).Returns("notes");
        module.Setup(m => m.GetSearchableDocumentAsync("note-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDocument("notes", "note-3", "updated title", "completely new content about finances"));

        var (indexingService, sp) = CreateIndexingService(provider, null, module.Object);
        using var _sp = sp;

        var handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance, provider, indexingService);

        indexingService.Start();
        try
        {
            await handler.HandleAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "notes",
                EntityId = "note-3",
                Action = SearchIndexAction.Index
            });

            // Allow background processing to complete
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100);
                if (indexingService.TotalProcessed > 0) break;
            }

            // Verify old content is gone
            var oldResult = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "original",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });
            Assert.AreEqual(0, oldResult.TotalCount, "Old content should not appear");

            // Verify new content appears
            var newResult = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "finances",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });
            Assert.IsTrue(newResult.TotalCount >= 1, "Updated content should appear");
            Assert.AreEqual("updated title", newResult.Items[0].Title.Replace("<mark>", "").Replace("</mark>", ""));
        }
        finally
        {
            await indexingService.StopAsync();
        }
    }

    [TestMethod]
    public async Task MultiModuleIndexing_AllModulesSearchable()
    {
        using var db = CreateDbContext(nameof(MultiModuleIndexing_AllModulesSearchable));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Index documents from multiple modules
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Budget Report", "quarterly budget report"));
        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "Budget.xlsx", "quarterly budget spreadsheet"));
        await provider.IndexDocumentAsync(CreateDocument("chat", "m1", "Budget Discussion", "quarterly budget review meeting"));
        await provider.IndexDocumentAsync(CreateDocument("calendar", "e1", "Budget Meeting", "quarterly budget review"));

        var query = new SearchQuery
        {
            QueryText = "quarterly budget",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(4, result.TotalCount);
        Assert.AreEqual(4, result.FacetCounts.Values.Sum());
        Assert.IsTrue(result.FacetCounts.ContainsKey("notes"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("files"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("chat"));
        Assert.IsTrue(result.FacetCounts.ContainsKey("calendar"));
    }

    [TestMethod]
    public async Task FullReindex_AllDocumentsReindexed()
    {
        var dbName = nameof(FullReindex_AllDocumentsReindexed);
        using var db = CreateDbContext(dbName);
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var notesModule = new Mock<ISearchableModule>();
        notesModule.Setup(m => m.ModuleId).Returns("notes");
        notesModule.Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDocument("notes", "n1", "Note 1", "first note content"),
                CreateDocument("notes", "n2", "Note 2", "second note content"),
                CreateDocument("notes", "n3", "Note 3", "third note content"),
            });

        var filesModule = new Mock<ISearchableModule>();
        filesModule.Setup(m => m.ModuleId).Returns("files");
        filesModule.Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDocument("files", "f1", "File 1", "file content"),
                CreateDocument("files", "f2", "File 2", "file content two"),
            });

        // Build a service scope factory
        var services = new ServiceCollection();
        services.AddDbContext<SearchDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISearchProvider>(provider);
        services.AddSingleton<ISearchableModule>(notesModule.Object);
        services.AddSingleton<ISearchableModule>(filesModule.Object);
        var sp = services.BuildServiceProvider();

        var reindexService = new SearchReindexBackgroundService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchReindexBackgroundService>.Instance);

        await reindexService.PerformFullReindexAsync();

        var stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(5, stats.TotalDocuments);
        Assert.AreEqual(3, stats.DocumentsPerModule["notes"]);
        Assert.AreEqual(2, stats.DocumentsPerModule["files"]);

        // Verify documents are searchable
        var result = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "content",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });
        Assert.AreEqual(5, result.TotalCount);
    }

    [TestMethod]
    public async Task ModuleReindex_ReindexesOnlyTargetedModule()
    {
        var dbName = nameof(ModuleReindex_ReindexesOnlyTargetedModule);
        using var db = CreateDbContext(dbName);
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Pre-index docs from two modules
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note 1", "existing note"));
        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "File 1", "existing file"));

        // Notes module returns updated docs
        var notesModule = new Mock<ISearchableModule>();
        notesModule.Setup(m => m.ModuleId).Returns("notes");
        notesModule.Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDocument("notes", "n1", "Updated Note 1", "updated note content"),
                CreateDocument("notes", "n2", "New Note 2", "brand new note"),
            });

        var services = new ServiceCollection();
        services.AddDbContext<SearchDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISearchProvider>(provider);
        services.AddSingleton<ISearchableModule>(notesModule.Object);
        var sp = services.BuildServiceProvider();

        var reindexService = new SearchReindexBackgroundService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchReindexBackgroundService>.Instance);

        await reindexService.PerformModuleReindexAsync("notes");

        var stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(3, stats.TotalDocuments); // 2 notes + 1 file (untouched)
        Assert.AreEqual(2, stats.DocumentsPerModule["notes"]);
        Assert.AreEqual(1, stats.DocumentsPerModule["files"]);
    }

    [TestMethod]
    public async Task ContentExtraction_PlainText_IntegratesWithIndexing()
    {
        using var db = CreateDbContext(nameof(ContentExtraction_PlainText_IntegratesWithIndexing));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var extractor = new Extractors.PlainTextExtractor();
        var (indexingService, sp) = CreateIndexingService(provider, [extractor]);
        using var _sp = sp;

        // Simulate extracting and indexing a text file
        using var stream = new MemoryStream("Important quarterly financial report data"u8.ToArray());
        var doc = CreateDocument("files", "f1", "report.txt", "");

        var enriched = await indexingService.EnrichDocumentFromStreamAsync(doc, stream, "text/plain");

        Assert.IsTrue(enriched.Content.Contains("quarterly financial"));
        Assert.AreEqual("report.txt", enriched.Title);
    }

    [TestMethod]
    public async Task ContentExtraction_Markdown_IntegratesWithIndexing()
    {
        using var db = CreateDbContext(nameof(ContentExtraction_Markdown_IntegratesWithIndexing));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var extractor = new Extractors.MarkdownContentExtractor();
        var (indexingService, sp) = CreateIndexingService(provider, [extractor]);
        using var _sp = sp;

        var markdown = "# Budget Report\n\n**Q4 Results:**\n\n- Revenue: $1M\n- Expenses: $800K\n\n[Link](http://example.com)";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
        var doc = CreateDocument("files", "f1", "report.md", "");

        var enriched = await indexingService.EnrichDocumentFromStreamAsync(doc, stream, "text/markdown");

        Assert.IsTrue(enriched.Content.Contains("Budget Report"));
        Assert.IsTrue(enriched.Content.Contains("Revenue"));
        Assert.IsFalse(enriched.Content.Contains("**"));
        Assert.IsFalse(enriched.Content.Contains("[Link]"));
    }

    [TestMethod]
    public async Task IndexingService_EntityDeletedBeforeProcessing_RemovesFromIndex()
    {
        using var db = CreateDbContext(nameof(IndexingService_EntityDeletedBeforeProcessing_RemovesFromIndex));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Pre-index a document
        await provider.IndexDocumentAsync(CreateDocument("notes", "note-5", "Temp Note", "temporary content"));

        // Module returns null (entity deleted)
        var module = new Mock<ISearchableModule>();
        module.Setup(m => m.ModuleId).Returns("notes");
        module.Setup(m => m.GetSearchableDocumentAsync("note-5", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchDocument?)null);

        var (indexingService, sp) = CreateIndexingService(provider, null, module.Object);
        using var _sp = sp;

        indexingService.Start();
        try
        {
            await indexingService.EnqueueAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "notes",
                EntityId = "note-5",
                Action = SearchIndexAction.Index
            });

            // Allow background processing to complete
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100);
                if (indexingService.TotalProcessed > 0) break;
            }

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "temporary",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(0, result.TotalCount, "Document should be removed when entity no longer exists");
        }
        finally
        {
            await indexingService.StopAsync();
        }
    }

    [TestMethod]
    public async Task ReindexCleanup_OrphanedModuleEntries_Removed()
    {
        var dbName = nameof(ReindexCleanup_OrphanedModuleEntries_Removed);
        using var db = CreateDbContext(dbName);
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Index docs for two modules
        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Note", "content"));
        await provider.IndexDocumentAsync(CreateDocument("deprecated_module", "d1", "Old", "orphaned content"));

        // Only register notes module
        var notesModule = new Mock<ISearchableModule>();
        notesModule.Setup(m => m.ModuleId).Returns("notes");
        notesModule.Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>
            {
                CreateDocument("notes", "n1", "Note", "content"),
            });

        var services = new ServiceCollection();
        services.AddDbContext<SearchDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISearchProvider>(provider);
        services.AddSingleton<ISearchableModule>(notesModule.Object);
        var sp = services.BuildServiceProvider();

        var reindexService = new SearchReindexBackgroundService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchReindexBackgroundService>.Instance);

        await reindexService.PerformFullReindexAsync();

        var stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(1, stats.TotalDocuments, "Orphaned module entries should be cleaned up");
        Assert.AreEqual(1, stats.DocumentsPerModule["notes"]);
        Assert.IsFalse(stats.DocumentsPerModule.ContainsKey("deprecated_module"));
    }

    [TestMethod]
    public async Task QueryService_FullPipeline_ParsesAndSearches()
    {
        using var db = CreateDbContext(nameof(QueryService_FullPipeline_ParsesAndSearches));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Budget Overview", "quarterly budget review for Q4 of 2025"));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n2", "Draft Meeting Notes", "draft notes about quarterly plans"));
        await provider.IndexDocumentAsync(CreateDocument("files", "f1", "Budget.xlsx", "quarterly budget data excel"));

        var queryService = new SearchQueryService(provider, NullLogger<SearchQueryService>.Instance);

        // Search with module filter syntax
        var result = await queryService.SearchAsync(new SearchQuery
        {
            QueryText = "in:notes quarterly",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(2, result.TotalCount);
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "notes"));
    }

    [TestMethod]
    public async Task QueryService_ExclusionSyntax_FiltersResults()
    {
        using var db = CreateDbContext(nameof(QueryService_ExclusionSyntax_FiltersResults));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDocument("notes", "n1", "Final Report", "final budget analysis"));
        await provider.IndexDocumentAsync(CreateDocument("notes", "n2", "Draft Report", "draft budget analysis"));

        var queryService = new SearchQueryService(provider, NullLogger<SearchQueryService>.Instance);

        var result = await queryService.SearchAsync(new SearchQuery
        {
            QueryText = "budget -draft",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("n1", result.Items[0].EntityId);
    }
}
