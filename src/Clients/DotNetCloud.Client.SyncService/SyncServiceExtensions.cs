using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.SyncService.ContextManager;
using DotNetCloud.Client.SyncService.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Client.SyncService;

/// <summary>
/// DI registration for the DotNetCloud Sync Service.
/// </summary>
public static class SyncServiceExtensions
{
    /// <summary>
    /// Adds all sync-service components to the service collection:
    /// <see cref="ISyncContextManager"/>, <see cref="IIpcServer"/>, and the
    /// <see cref="SyncWorker"/> hosted service.
    /// </summary>
    public static IServiceCollection AddDotNetCloudSyncService(
        this IServiceCollection services)
    {
        // Named HttpClient used by per-context API clients.
        // Uses the same TLS bypass as OAuth2Service so self-signed certs
        // on local/private hosts (e.g. mint22) are accepted.
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient("DotNetCloudSync")
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler)
            .AddHttpMessageHandler<CorrelationIdHandler>();

        services.AddSingleton<ISyncContextManager, SyncContextManager>();
        services.AddSingleton<IIpcServer, IpcServer>();
        services.AddHostedService<SyncWorker>();

        return services;
    }
}
