using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Events;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// Phase 4 tests for <see cref="SearchIndexRequestEventHandler"/> — verifies Index actions
/// are forwarded to <see cref="SearchIndexingService"/> for queued processing.
/// </summary>
[TestClass]
public class SearchIndexRequestEventHandlerPhase4Tests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchIndexingService _realIndexingService = null!;
    private SearchIndexRequestEventHandler _handler = null!;
    private ServiceProvider _sp = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();

        var services = new ServiceCollection();
        services.AddScoped<ISearchProvider>(_ => _searchProviderMock.Object);
        services.AddScoped<ContentExtractionService>();
        services.AddSingleton<IEnumerable<IContentExtractor>>(Array.Empty<IContentExtractor>());
        services.AddLogging();
        _sp = services.BuildServiceProvider();

        _realIndexingService = new SearchIndexingService(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _realIndexingService.Dispose();
        _sp.Dispose();
    }

    [TestMethod]
    public async Task HandleAsync_IndexAction_EnqueuesToIndexingService()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object,
            _realIndexingService);

        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Index
        };

        await _handler.HandleAsync(@event);

        Assert.AreEqual(1, _realIndexingService.PendingCount);
    }

    [TestMethod]
    public async Task HandleAsync_RemoveAction_CallsSearchProviderDirectly()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object,
            _realIndexingService);

        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Remove
        };

        await _handler.HandleAsync(@event);

        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "entity-1", It.IsAny<CancellationToken>()),
            Times.Once);

        // Should NOT enqueue to indexing service
        Assert.AreEqual(0, _realIndexingService.PendingCount);
    }

    [TestMethod]
    public async Task HandleAsync_RemoveAction_NullProvider_DoesNotThrow()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            null,
            _realIndexingService);

        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Remove
        };

        await _handler.HandleAsync(@event);
        // No exception expected
    }

    [TestMethod]
    public async Task HandleAsync_IndexAction_NullIndexingService_DoesNotThrow()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object,
            null);

        var @event = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "entity-1",
            Action = SearchIndexAction.Index
        };

        await _handler.HandleAsync(@event);
        // No crash; just a log warning
    }

    [TestMethod]
    public async Task HandleAsync_MultipleIndexEvents_AllEnqueued()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object,
            _realIndexingService);

        for (int i = 0; i < 10; i++)
        {
            await _handler.HandleAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "files",
                EntityId = $"entity-{i}",
                Action = SearchIndexAction.Index
            });
        }

        Assert.AreEqual(10, _realIndexingService.PendingCount);
    }

    [TestMethod]
    public async Task HandleAsync_MixedActions_CorrectRouting()
    {
        _handler = new SearchIndexRequestEventHandler(
            NullLogger<SearchIndexRequestEventHandler>.Instance,
            _searchProviderMock.Object,
            _realIndexingService);

        // Index action
        await _handler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e1",
            Action = SearchIndexAction.Index
        });

        // Remove action
        await _handler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = "e2",
            Action = SearchIndexAction.Remove
        });

        // Another index
        await _handler.HandleAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "notes",
            EntityId = "n1",
            Action = SearchIndexAction.Index
        });

        // Two Index events should be enqueued
        Assert.AreEqual(2, _realIndexingService.PendingCount);

        // One Remove should go directly to provider
        _searchProviderMock.Verify(
            p => p.RemoveDocumentAsync("files", "e2", It.IsAny<CancellationToken>()),
            Times.Once);

        // No IndexDocument calls from handler (that's the indexing service's job)
        _searchProviderMock.Verify(
            p => p.IndexDocumentAsync(It.IsAny<Core.DTOs.Search.SearchDocument>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
