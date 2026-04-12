using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Events;

/// <summary>
/// Handles <see cref="SearchIndexRequestEvent"/> by forwarding to the search provider
/// for indexing or removal operations.
/// </summary>
public sealed class SearchIndexRequestEventHandler : IEventHandler<SearchIndexRequestEvent>
{
    private readonly ILogger<SearchIndexRequestEventHandler> _logger;
    private readonly ISearchProvider? _searchProvider;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexRequestEventHandler"/> class.</summary>
    public SearchIndexRequestEventHandler(
        ILogger<SearchIndexRequestEventHandler> logger,
        ISearchProvider? searchProvider)
    {
        _logger = logger;
        _searchProvider = searchProvider;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Received search index request: {Action} {ModuleId}/{EntityId}",
            @event.Action, @event.ModuleId, @event.EntityId);

        if (_searchProvider is null)
        {
            _logger.LogWarning("No search provider configured, skipping index request");
            return;
        }

        if (@event.Action == SearchIndexAction.Remove)
        {
            await _searchProvider.RemoveDocumentAsync(@event.ModuleId, @event.EntityId, cancellationToken);
        }

        // For Index action, the SearchIndexingService handles getting the document
        // from the searchable module and indexing it. This handler just logs the event.
    }
}
