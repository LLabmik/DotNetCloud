using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// Phase 4 tests for <see cref="SearchReindexBackgroundService"/> — scheduled full reindex,
/// module-specific reindex, batch processing, stale entry cleanup, and job tracking.
/// </summary>
[TestClass]
public class SearchReindexBackgroundServicePhase4Tests
{
    private ServiceProvider _serviceProvider = null!;
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private Mock<ISearchableModule> _filesModuleMock = null!;
    private Mock<ISearchableModule> _notesModuleMock = null!;
    private SearchReindexBackgroundService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();

        _filesModuleMock = new Mock<ISearchableModule>();
        _filesModuleMock.Setup(m => m.ModuleId).Returns("files");
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>());

        _notesModuleMock = new Mock<ISearchableModule>();
        _notesModuleMock.Setup(m => m.ModuleId).Returns("notes");
        _notesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchDocument>());

        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<SearchDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISearchProvider>(_searchProviderMock.Object);
        services.AddSingleton<ISearchableModule>(_filesModuleMock.Object);
        services.AddSingleton<ISearchableModule>(_notesModuleMock.Object);

        _serviceProvider = services.BuildServiceProvider();

        _service = new SearchReindexBackgroundService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchReindexBackgroundService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    // --- Full Reindex Tests ---

    [TestMethod]
    public async Task PerformFullReindexAsync_ReindexesAllModules()
    {
        await _service.PerformFullReindexAsync();

        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("files", It.IsAny<CancellationToken>()), Times.Once);
        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("notes", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_IndexesAllDocuments()
    {
        var fileDocs = CreateDocuments("files", "FileNode", 5);
        var noteDocs = CreateDocuments("notes", "Note", 3);

        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDocs);
        _notesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(noteDocs);

        await _service.PerformFullReindexAsync();

        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Exactly(8));
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_CreatesCompletedJob()
    {
        var fileDocs = CreateDocuments("files", "FileNode", 3);
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDocs);

        await _service.PerformFullReindexAsync();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();

        Assert.AreEqual(IndexJobType.Full, job.Type);
        Assert.AreEqual(IndexJobStatus.Completed, job.Status);
        Assert.AreEqual(3, job.DocumentsProcessed);
        Assert.AreEqual(3, job.DocumentsTotal);
        Assert.IsNotNull(job.StartedAt);
        Assert.IsNotNull(job.CompletedAt);
        Assert.IsNull(job.ErrorMessage);
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_EmptyModules_CreatesCompletedJobWithZeroDocs()
    {
        await _service.PerformFullReindexAsync();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();

        Assert.AreEqual(IndexJobStatus.Completed, job.Status);
        Assert.AreEqual(0, job.DocumentsProcessed);
        Assert.AreEqual(0, job.DocumentsTotal);
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_ModuleFailure_ContinuesWithOtherModules()
    {
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Files module error"));

        var noteDocs = CreateDocuments("notes", "Note", 2);
        _notesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(noteDocs);

        await _service.PerformFullReindexAsync();

        // Notes should still be indexed despite files failure
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();
        Assert.AreEqual(IndexJobStatus.Completed, job.Status);
        Assert.AreEqual(2, job.DocumentsProcessed);
    }

    // --- Module-Specific Reindex Tests ---

    [TestMethod]
    public async Task PerformModuleReindexAsync_IndexesOnlySpecifiedModule()
    {
        var fileDocs = CreateDocuments("files", "FileNode", 4);
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileDocs);

        await _service.PerformModuleReindexAsync("files");

        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("files", It.IsAny<CancellationToken>()), Times.Once);
        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("notes", It.IsAny<CancellationToken>()), Times.Never);
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [TestMethod]
    public async Task PerformModuleReindexAsync_CreatesIncrementalJob()
    {
        var docs = CreateDocuments("files", "FileNode", 2);
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);

        await _service.PerformModuleReindexAsync("files");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();

        Assert.AreEqual(IndexJobType.Incremental, job.Type);
        Assert.AreEqual(IndexJobStatus.Completed, job.Status);
        Assert.AreEqual("files", job.ModuleId);
        Assert.AreEqual(2, job.DocumentsProcessed);
        Assert.AreEqual(2, job.DocumentsTotal);
    }

    [TestMethod]
    public async Task PerformModuleReindexAsync_UnknownModule_DoesNothing()
    {
        await _service.PerformModuleReindexAsync("nonexistent");

        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()), Times.Never);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var jobCount = await db.IndexingJobs.CountAsync();
        Assert.AreEqual(0, jobCount);
    }

    [TestMethod]
    public async Task PerformModuleReindexAsync_Failure_CreatesFailedJob()
    {
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.PerformModuleReindexAsync("files"));

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();

        Assert.AreEqual(IndexJobStatus.Failed, job.Status);
        Assert.AreEqual("Connection failed", job.ErrorMessage);
        Assert.IsNotNull(job.CompletedAt);
    }

    // --- Batch Processing Tests ---

    [TestMethod]
    public async Task PerformFullReindexAsync_LargeDocumentSet_ProcessesInBatches()
    {
        // Create more docs than one batch (DefaultBatchSize = 200)
        var docs = CreateDocuments("files", "FileNode", 450);
        _filesModuleMock
            .Setup(m => m.GetAllSearchableDocumentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(docs);

        await _service.PerformFullReindexAsync();

        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Exactly(450));

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();
        Assert.AreEqual(450, job.DocumentsProcessed);
    }

    [TestMethod]
    public async Task DefaultBatchSize_Is200()
    {
        Assert.AreEqual(200, SearchReindexBackgroundService.DefaultBatchSize);
    }

    // --- Stale Entry Cleanup Tests ---

    [TestMethod]
    public async Task CleanupOrphanedEntriesAsync_RemovesEntriesFromUnregisteredModules()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();

        // Add entries for a module that is no longer registered
        db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "deleted-module",
            EntityId = "e1",
            EntityType = "Widget",
            Title = "Orphan",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IndexedAt = DateTimeOffset.UtcNow
        });

        // Add entries for registered modules
        db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "files",
            EntityId = "f1",
            EntityType = "FileNode",
            Title = "Good File",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IndexedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var registeredModules = new[] { _filesModuleMock.Object, _notesModuleMock.Object };

        await SearchReindexBackgroundService.CleanupOrphanedEntriesAsync(
            db, registeredModules, CancellationToken.None);

        var remaining = await db.SearchIndexEntries.ToListAsync();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual("files", remaining[0].ModuleId);
    }

    [TestMethod]
    public async Task CleanupOrphanedEntriesAsync_NoOrphans_NothingRemoved()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();

        db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "files",
            EntityId = "f1",
            EntityType = "FileNode",
            Title = "File",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IndexedAt = DateTimeOffset.UtcNow
        });
        db.SearchIndexEntries.Add(new SearchIndexEntry
        {
            ModuleId = "notes",
            EntityId = "n1",
            EntityType = "Note",
            Title = "Note",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IndexedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var registeredModules = new[] { _filesModuleMock.Object, _notesModuleMock.Object };

        await SearchReindexBackgroundService.CleanupOrphanedEntriesAsync(
            db, registeredModules, CancellationToken.None);

        var count = await db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task CleanupOrphanedEntriesAsync_EmptyIndex_DoesNothing()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();

        var registeredModules = new[] { _filesModuleMock.Object };

        await SearchReindexBackgroundService.CleanupOrphanedEntriesAsync(
            db, registeredModules, CancellationToken.None);

        var count = await db.SearchIndexEntries.CountAsync();
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_CleansUpOrphanedEntries()
    {
        // Pre-fill orphaned entries in DB
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
            db.SearchIndexEntries.Add(new SearchIndexEntry
            {
                ModuleId = "obsolete-module",
                EntityId = "o1",
                EntityType = "Widget",
                Title = "Orphan",
                OwnerId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IndexedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await _service.PerformFullReindexAsync();

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
            var entries = await db.SearchIndexEntries
                .Where(e => e.ModuleId == "obsolete-module")
                .ToListAsync();
            Assert.AreEqual(0, entries.Count);
        }
    }

    // --- Manual Trigger Tests ---

    [TestMethod]
    public void TriggerFullReindex_DoesNotThrow()
    {
        _service.TriggerFullReindex();
    }

    [TestMethod]
    public void TriggerModuleReindex_DoesNotThrow()
    {
        _service.TriggerModuleReindex("files");
    }

    // --- Job Tracking Tests ---

    [TestMethod]
    public async Task PerformFullReindexAsync_MultipleRuns_CreatesMultipleJobs()
    {
        await _service.PerformFullReindexAsync();
        await _service.PerformFullReindexAsync();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var jobCount = await db.IndexingJobs.CountAsync();
        Assert.AreEqual(2, jobCount);
    }

    [TestMethod]
    public async Task PerformFullReindexAsync_JobCompletedAt_AfterStartedAt()
    {
        await _service.PerformFullReindexAsync();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
        var job = await db.IndexingJobs.FirstAsync();

        Assert.IsNotNull(job.StartedAt);
        Assert.IsNotNull(job.CompletedAt);
        Assert.IsTrue(job.CompletedAt >= job.StartedAt);
    }

    // --- Helpers ---

    private static List<SearchDocument> CreateDocuments(string moduleId, string entityType, int count)
    {
        return Enumerable.Range(1, count).Select(i => new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = $"{moduleId}-e{i}",
            EntityType = entityType,
            Title = $"{entityType} {i}",
            Content = $"Content for {entityType} {i}",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();
    }
}
