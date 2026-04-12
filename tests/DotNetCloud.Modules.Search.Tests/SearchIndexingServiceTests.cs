using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchIndexingService"/>.
/// </summary>
[TestClass]
public class SearchIndexingServiceTests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private Mock<ISearchableModule> _searchableModuleMock = null!;
    private SearchIndexingService _service = null!;
    private ContentExtractionService _extractionService = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _searchableModuleMock = new Mock<ISearchableModule>();
        _searchableModuleMock.Setup(m => m.ModuleId).Returns("files");

        _extractionService = new ContentExtractionService(
            [],
            NullLogger<ContentExtractionService>.Instance);

        _service = new SearchIndexingService(
            _searchProviderMock.Object,
            [_searchableModuleMock.Object],
            _extractionService,
            NullLogger<SearchIndexingService>.Instance);
    }

    [TestMethod]
    public async Task EnqueueAsync_AddsToQueue()
    {
        var request = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e1",
            Action = SearchIndexAction.Index
        };

        await _service.EnqueueAsync(request);

        Assert.AreEqual(1, _service.PendingCount);
    }

    [TestMethod]
    public async Task ProcessQueue_RemoveAction_CallsRemoveDocument()
    {
        _service.Start();

        var request = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e1",
            Action = SearchIndexAction.Remove
        };

        await _service.EnqueueAsync(request);

        // Allow time for processing
        await Task.Delay(200);
        await _service.StopAsync();

        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "e1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessQueue_IndexAction_GetsDocumentAndIndexes()
    {
        var doc = new SearchDocument
        {
            ModuleId = "files",
            EntityId = "e1",
            EntityType = "FileNode",
            Title = "Test File",
            Content = "test content",
            OwnerId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _searchableModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        _service.Start();

        var request = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e1",
            Action = SearchIndexAction.Index
        };

        await _service.EnqueueAsync(request);

        await Task.Delay(200);
        await _service.StopAsync();

        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(doc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessQueue_IndexAction_NoModule_LogsWarning()
    {
        // Use a module ID that doesn't match any registered searchable module
        _service.Start();

        var request = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "unknown-module",
            EntityId = "e1",
            Action = SearchIndexAction.Index
        };

        await _service.EnqueueAsync(request);

        await Task.Delay(200);
        await _service.StopAsync();

        // Should not throw, and should not index anything
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProcessQueue_IndexAction_DocumentNotFound_RemovesFromIndex()
    {
        _searchableModuleMock
            .Setup(m => m.GetSearchableDocumentAsync("e1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchDocument?)null);

        _service.Start();

        var request = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e1",
            Action = SearchIndexAction.Index
        };

        await _service.EnqueueAsync(request);

        await Task.Delay(200);
        await _service.StopAsync();

        // When entity no longer exists, it should be removed from the index
        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "e1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void PendingCount_InitiallyZero()
    {
        Assert.AreEqual(0, _service.PendingCount);
    }

    [TestMethod]
    public void Dispose_DoesNotThrow()
    {
        _service.Dispose();
    }

    [TestMethod]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        await _service.StopAsync();
    }
}
