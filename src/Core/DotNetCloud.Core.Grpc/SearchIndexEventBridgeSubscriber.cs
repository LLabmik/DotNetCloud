using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Subscribes the module host's local <see cref="IEventBus"/> to
/// <see cref="SearchIndexRequestEvent"/> and forwards each event to Core.Server over gRPC.
/// </summary>
internal sealed class SearchIndexEventBridgeSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly SearchIndexEventBridgeHandler _handler;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexEventBridgeSubscriber"/> class.</summary>
    public SearchIndexEventBridgeSubscriber(
        IEventBus eventBus,
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<SearchIndexEventBridgeHandler> logger)
    {
        _eventBus = eventBus;
        _handler = new SearchIndexEventBridgeHandler(coreClient, logger);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventBus.UnsubscribeAsync<SearchIndexRequestEvent>(_handler, cancellationToken);
    }
}
