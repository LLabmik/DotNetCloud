using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes a search index event handler to the event bus on startup
/// and performs an initial search index build for existing data.
/// Document retrieval from each module and indexing both use gRPC calls
/// — no in-process ISearchableModule or ISearchProvider registrations needed.
/// </summary>
internal sealed class SearchEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly ISearchApiClient _searchClient;
    private readonly IEnumerable<IModuleSearchDocumentClient> _documentClients;
    private readonly ILogger<SearchEventSubscriber> _logger;
    private SearchIndexEventHandler? _handler;

    /// <summary>Initializes a new instance of the <see cref="SearchEventSubscriber"/> class.</summary>
    public SearchEventSubscriber(
        IEventBus eventBus,
        ISearchApiClient searchClient,
        IEnumerable<IModuleSearchDocumentClient> documentClients,
        ILogger<SearchEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _searchClient = searchClient;
        _documentClients = documentClients;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = new SearchIndexEventHandler(_searchClient, _documentClients, _logger);
        await _eventBus.SubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);

        _logger.LogInformation("Search event subscriber started — gRPC document clients + gRPC search indexing");

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
        const int maxRetries = 10;
        const int retryDelaySeconds = 15;

        // Wait for all module hosts to start their gRPC servers before attempting connections.
        // Module process spawns take 30-90 seconds depending on system load.
        _logger.LogInformation("Waiting 90s for module hosts to start before initial search index build");
        await Task.Delay(TimeSpan.FromSeconds(90), cancellationToken);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var clientList = _documentClients.ToList();
                _logger.LogInformation("Performing initial search index build for {Count} modules via gRPC (attempt {Attempt}/{MaxRetries})",
                    clientList.Count, attempt, maxRetries);

                int totalIndexed = 0;
                bool anyModuleResponded = false;

                foreach (var client in clientList)
                {
                    try
                    {
                        var docs = await client.GetAllSearchableDocumentsAsync(cancellationToken);
                        anyModuleResponded = true;
                        _logger.LogInformation("Indexing {Count} documents from module {ModuleId} via gRPC",
                            docs.Count, client.ModuleId);

                        foreach (var doc in docs)
                        {
                            await _searchClient.IndexDocumentAsync(doc, cancellationToken);
                            totalIndexed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to index module {ModuleId} during initial build via gRPC", client.ModuleId);
                    }
                }

                if (anyModuleResponded && attempt >= 3)
                {
                    // After 3 attempts with at least one module responding, consider the build complete.
                    var finalStats = await _searchClient.GetIndexStatsAsync(cancellationToken);
                    _logger.LogInformation("Initial search index build complete — {Count} documents indexed via gRPC",
                        finalStats.TotalDocuments);
                    return;
                }

                if (attempt < maxRetries)
                {
                    _logger.LogWarning("No modules responded on attempt {Attempt}/{MaxRetries}, retrying in {Delay}s",
                        attempt, maxRetries, retryDelaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                }
                else
                {
                    _logger.LogError("Initial search index build failed after {MaxRetries} attempts",
                        maxRetries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during initial search index build attempt {Attempt}/{MaxRetries}",
                    attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                }
                else
                {
                    _logger.LogError(ex, "Error during initial search index build after {MaxRetries} attempts",
                        maxRetries);
                }
            }
        }
    }
}

/// <summary>
/// Event handler that resolves the right module's gRPC client to get a searchable document,
/// then indexes or removes it via the Search module's gRPC service.
/// No longer depends on ISearchableModule or ISearchProvider DI registrations.
/// </summary>
internal sealed class SearchIndexEventHandler : DotNetCloud.Core.Events.IEventHandler<SearchIndexRequestEvent>
{
    private readonly ISearchApiClient _searchClient;
    private readonly IEnumerable<IModuleSearchDocumentClient> _documentClients;
    private readonly ILogger _logger;

    public SearchIndexEventHandler(
        ISearchApiClient searchClient,
        IEnumerable<IModuleSearchDocumentClient> documentClients,
        ILogger logger)
    {
        _searchClient = searchClient;
        _documentClients = documentClients;
        _logger = logger;
    }

    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.Action == SearchIndexAction.Remove)
        {
            await _searchClient.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
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
            await _searchClient.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
            return;
        }

        await _searchClient.IndexDocumentAsync(document, cancellationToken);
    }
}
