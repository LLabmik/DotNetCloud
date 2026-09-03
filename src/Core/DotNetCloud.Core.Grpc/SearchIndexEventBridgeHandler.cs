using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Forwards a <see cref="SearchIndexRequestEvent"/> to Core.Server so the search index
/// updates in near-real-time when a module's searchable content changes.
/// </summary>
internal sealed class SearchIndexEventBridgeHandler : IEventHandler<SearchIndexRequestEvent>
{
    private readonly CoreCapabilities.CoreCapabilitiesClient _coreClient;
    private readonly string _moduleId;
    private readonly ILogger<SearchIndexEventBridgeHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="SearchIndexEventBridgeHandler"/> class.</summary>
    public SearchIndexEventBridgeHandler(
        CoreCapabilities.CoreCapabilitiesClient coreClient,
        ILogger<SearchIndexEventBridgeHandler> logger)
    {
        _coreClient = coreClient;
        // The ProcessSupervisor sets this when it launches the module host. Core.Server's
        // AuthenticationInterceptor rejects gRPC calls without a module-id header.
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(SearchIndexRequestEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var headers = new Metadata { { "module-id", _moduleId } };
            var response = await _coreClient.SubmitSearchIndexAsync(
                new SubmitSearchIndexRequest
                {
                    ModuleId = @event.ModuleId,
                    EntityId = @event.EntityId,
                    Action = (int)@event.Action
                },
                headers,
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "Core.Server rejected search index request for {ModuleId}/{EntityId} action {Action}",
                    @event.ModuleId, @event.EntityId, @event.Action);
            }
        }
        catch (Exception ex)
        {
            // Real-time indexing must never break module CRUD operations.
            _logger.LogWarning(ex,
                "Failed to submit search index request for {ModuleId}/{EntityId} action {Action}",
                @event.ModuleId, @event.EntityId, @event.Action);
        }
    }
}
