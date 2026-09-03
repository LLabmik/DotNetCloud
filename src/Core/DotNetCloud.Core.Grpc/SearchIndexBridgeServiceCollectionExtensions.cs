using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for the real-time search indexing bridge used by process-isolated
/// module hosts. Mirrors <see cref="AuditLoggerServiceCollectionExtensions"/>.
/// </summary>
public static class SearchIndexBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="CoreCapabilities.CoreCapabilitiesClient"/> and a hosted-service
    /// subscriber that forwards <see cref="DotNetCloud.Core.Events.Search.SearchIndexRequestEvent"/>
    /// to Core.Server. No-op when <c>DOTNETCLOUD_CORE_ENDPOINT</c> is absent (standalone/test host).
    /// </summary>
    public static IServiceCollection AddSearchIndexBridge(this IServiceCollection services)
    {
        var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(coreEndpoint))
            return services;

        // TryAddSingleton so hosts that already register a CoreCapabilitiesClient
        // (e.g. Calendar) don't end up with two clients.
        services.TryAddSingleton(_ =>
        {
            var channel = GrpcChannel.ForAddress(coreEndpoint);
            return new CoreCapabilities.CoreCapabilitiesClient(channel);
        });

        services.AddHostedService<SearchIndexEventBridgeSubscriber>();
        return services;
    }
}
