using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Events;

/// <summary>
/// Handles <see cref="SearchIndexRequestEvent"/> by forwarding to the <see cref="SearchIndexingService"/>
/// for queued background processing. Remove actions are also forwarded directly to the search provider
/// for immediate removal.
/// </summary>
public sealed class SearchIndexRequestEventHandler : IEventHandler<SearchIndexRequestEvent>
{
    private readonly ILogger<SearchIndexRequestEventHandler> _logger;
    private readonly ISearchProvider? _searchProvider;
    private readonly SearchIndexingService? _indexingService;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexRequestEventHandler"/> class.</summary>
    public SearchIndexRequestEventHandler(
        ILogger<SearchIndexRequestEventHandler> logger,
        ISearchProvider? searchProvider,
        SearchIndexingService? indexingService = null)
    {
        _logger = logger;
        _searchProvider = searchProvider;
        _indexingService = indexingService;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Received search index request: {Action} {ModuleId}/{EntityId}",
            @event.Action, @event.ModuleId, @event.EntityId);

        if (@event.Action == SearchIndexAction.Remove)
        {
            if (_searchProvider is null)
            {
                _logger.LogWarning("No search provider configured, skipping remove request");
                return;
            }

            await _searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
            return;
        }

        // For Index actions, enqueue to the SearchIndexingService for background processing
        if (_indexingService is not null)
        {
            await _indexingService.EnqueueAsync(@event, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No indexing service configured, skipping index request for {ModuleId}/{EntityId}",
                @event.ModuleId, @event.EntityId);
        }
    }
}
