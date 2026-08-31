using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes a search index event handler to the event bus on startup.
/// Document retrieval from each module uses gRPC via <see cref="IModuleSearchDocumentClient"/>,
/// and indexing/removal go directly to the core-owned <see cref="ISearchProvider"/> (CoreDbContext).
/// The startup full-index responsibility moved to <see cref="SearchReindexHostedService"/>.
/// </summary>
internal sealed class SearchEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IModuleSearchDocumentClient> _documentClients;
    private readonly ILogger<SearchEventSubscriber> _logger;
    private SearchIndexEventHandler? _handler;

    /// <summary>Initializes a new instance of the <see cref="SearchEventSubscriber"/> class.</summary>
    public SearchEventSubscriber(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IModuleSearchDocumentClient> documentClients,
        ILogger<SearchEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _documentClients = documentClients;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = new SearchIndexEventHandler(_scopeFactory, _documentClients, _logger);
        await _eventBus.SubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);

        _logger.LogInformation("Search event subscriber started — gRPC document clients + core search provider");
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
}

/// <summary>
/// Event handler that resolves the right module's gRPC client to get a searchable document,
/// then indexes or removes it via the core-owned <see cref="ISearchProvider"/>.
/// The provider is scoped, so it is resolved per-event from a fresh scope.
/// </summary>
internal sealed class SearchIndexEventHandler : IEventHandler<SearchIndexRequestEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IModuleSearchDocumentClient> _documentClients;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexEventHandler"/> class.</summary>
    public SearchIndexEventHandler(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IModuleSearchDocumentClient> documentClients,
        ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _documentClients = documentClients;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var searchProvider = scope.ServiceProvider.GetRequiredService<ISearchProvider>();

        if (@event.Action == SearchIndexAction.Remove)
        {
            await searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
            return;
        }

        // Find the right module's gRPC client by module ID
        var client = _documentClients.FirstOrDefault(c => c.ModuleId == @event.ModuleId);
        if (client is null)
        {
            _logger.LogWarning("No gRPC document client found for module {ModuleId}", @event.ModuleId);
            return;
        }

        var document = await client.GetSearchableDocumentAsync(@event.EntityId, cancellationToken);
        if (document is null)
        {
            // Entity was deleted — remove from index
            await searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
            return;
        }

        await searchProvider.IndexDocumentAsync(document, cancellationToken);
    }
}
