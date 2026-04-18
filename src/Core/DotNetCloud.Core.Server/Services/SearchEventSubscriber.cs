using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes a search index event handler to the event bus on startup
/// and performs an initial search index build for existing data.
/// Real-time indexing uses the event handler to index/remove documents directly via <see cref="ISearchProvider"/>.
/// </summary>
internal sealed class SearchEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchEventSubscriber> _logger;
    private ScopedSearchIndexHandler? _handler;

    /// <summary>Initializes a new instance of the <see cref="SearchEventSubscriber"/> class.</summary>
    public SearchEventSubscriber(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<SearchEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = new ScopedSearchIndexHandler(_scopeFactory);
        await _eventBus.SubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);

        _logger.LogInformation("Search event subscriber started");

        // Perform initial index build in the background so startup isn't blocked
        _ = Task.Run(() => PerformInitialIndexAsync(CancellationToken.None), CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_handler is not null)
        {
            try
            {
                await _eventBus.UnsubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unsubscribing search event handler");
            }
        }

        _logger.LogInformation("Search event subscriber stopped");
    }

    private async Task PerformInitialIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();
            var searchableModules = scope.ServiceProvider.GetServices<ISearchableModule>();

            // Check if the index already has data — skip if so
            var stats = await searchProvider.GetIndexStatsAsync(cancellationToken);
            if (stats.TotalDocuments > 0)
            {
                _logger.LogInformation("Search index already contains {Count} documents, skipping initial index build",
                    stats.TotalDocuments);
                return;
            }

            var moduleList = searchableModules.ToList();
            _logger.LogInformation("Performing initial search index build for {Count} modules", moduleList.Count);

            foreach (var module in moduleList)
            {
                try
                {
                    var docs = await module.GetAllSearchableDocumentsAsync(cancellationToken);
                    _logger.LogInformation("Indexing {Count} documents from module {ModuleId}",
                        docs.Count, module.ModuleId);

                    foreach (var doc in docs)
                    {
                        await searchProvider.IndexDocumentAsync(doc, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index module {ModuleId} during initial build", module.ModuleId);
                }
            }

            var finalStats = await searchProvider.GetIndexStatsAsync(cancellationToken);
            _logger.LogInformation("Initial search index build complete — {Count} documents indexed",
                finalStats.TotalDocuments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial search index build");
        }
    }

    /// <summary>
    /// Event handler that creates a DI scope per event to resolve scoped services (ISearchProvider, ISearchableModule).
    /// This ensures proper DbContext lifecycle management.
    /// </summary>
    private sealed class ScopedSearchIndexHandler : DotNetCloud.Core.Events.IEventHandler<SearchIndexRequestEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ScopedSearchIndexHandler(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();

            if (@event.Action == SearchIndexAction.Remove)
            {
                await searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
                return;
            }

            // For index actions, find the module and pull the document
            var searchableModules = scope.ServiceProvider.GetServices<ISearchableModule>();
            var module = searchableModules.FirstOrDefault(m => m.ModuleId == @event.ModuleId);
            if (module is null)
                return;

            var document = await module.GetSearchableDocumentAsync(@event.EntityId, cancellationToken);
            if (document is null)
            {
                // Entity was deleted — remove from index
                await searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
                return;
            }

            await searchProvider.IndexDocumentAsync(document, cancellationToken);
        }
    }
}
