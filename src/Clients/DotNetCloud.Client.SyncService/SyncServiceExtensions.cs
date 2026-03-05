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
        // Named HttpClient used by per-context API clients
        services.AddHttpClient("DotNetCloudSync");

        services.AddSingleton<ISyncContextManager, SyncContextManager>();
        services.AddSingleton<IIpcServer, IpcServer>();
        services.AddHostedService<SyncWorker>();

        return services;
    }
}
