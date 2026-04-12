using System.Threading.Channels;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Events.Search;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// Processes search indexing requests using a background channel for backpressure.
/// Receives <see cref="SearchIndexRequestEvent"/> events and calls the search provider
/// to update the index.
/// </summary>
public sealed class SearchIndexingService : IDisposable
{
    private readonly ISearchProvider _searchProvider;
    private readonly IEnumerable<ISearchableModule> _searchableModules;
    private readonly ContentExtractionService _extractionService;
    private readonly ILogger<SearchIndexingService> _logger;
    private readonly Channel<SearchIndexRequestEvent> _channel;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexingService"/> class.</summary>
    public SearchIndexingService(
        ISearchProvider searchProvider,
        IEnumerable<ISearchableModule> searchableModules,
        ContentExtractionService extractionService,
        ILogger<SearchIndexingService> logger)
    {
        _searchProvider = searchProvider;
        _searchableModules = searchableModules;
        _extractionService = extractionService;
        _logger = logger;
        _channel = Channel.CreateBounded<SearchIndexRequestEvent>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>Starts the background processing loop.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _processingTask = ProcessQueueAsync(_cts.Token);
    }

    /// <summary>Stops the background processing loop.</summary>
    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    /// <summary>Enqueues an indexing request for background processing.</summary>
    public async ValueTask EnqueueAsync(SearchIndexRequestEvent request, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>Gets the number of pending items in the queue.</summary>
    public int PendingCount => _channel.Reader.Count;

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Search indexing service started processing");

        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessRequestAsync(request, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error processing index request for {ModuleId}/{EntityId}",
                        request.ModuleId, request.EntityId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Search indexing service stopping");
        }
    }

    private async Task ProcessRequestAsync(SearchIndexRequestEvent request, CancellationToken cancellationToken)
    {
        if (request.Action == SearchIndexAction.Remove)
        {
            await _searchProvider.RemoveDocumentAsync(request.ModuleId, request.EntityId, cancellationToken);
            _logger.LogDebug("Removed {ModuleId}/{EntityId} from index", request.ModuleId, request.EntityId);
            return;
        }

        // Find the module that owns this entity
        var module = _searchableModules.FirstOrDefault(m => m.ModuleId == request.ModuleId);
        if (module is null)
        {
            _logger.LogWarning("No searchable module found for {ModuleId}", request.ModuleId);
            return;
        }

        // Get the document from the module
        var document = await module.GetSearchableDocumentAsync(request.EntityId, cancellationToken);
        if (document is null)
        {
            // Entity no longer exists — remove from index
            await _searchProvider.RemoveDocumentAsync(request.ModuleId, request.EntityId, cancellationToken);
            _logger.LogDebug("Entity {ModuleId}/{EntityId} no longer exists, removed from index",
                request.ModuleId, request.EntityId);
            return;
        }

        await _searchProvider.IndexDocumentAsync(document, cancellationToken);
        _logger.LogDebug("Indexed {ModuleId}/{EntityId}", request.ModuleId, request.EntityId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Dispose();
    }
}
