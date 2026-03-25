using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core;

/// <summary>
/// DI registration for the SyncContextManager and its dependencies.
/// Used by the SyncTray app to register the sync context manager directly
/// (no separate process or IPC layer required).
/// </summary>
public static class SyncContextManagerExtensions
{
    /// <summary>
    /// Adds the <see cref="ISyncContextManager"/> and its required HTTP client
    /// pipeline to the service collection.
    /// </summary>
    public static IServiceCollection AddSyncContextManager(
        this IServiceCollection services)
    {
        // Named HttpClient used by per-context API clients.
        // Uses the same TLS bypass as OAuth2Service so self-signed certs
        // on local/private hosts (e.g. mint22) are accepted.
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient(sp =>
        {
            var dataRoot = SyncContextManager.GetSystemDataRoot();
            var deviceIdProvider = new DeviceIdProvider(sp.GetRequiredService<ILogger<DeviceIdProvider>>());
            var deviceId = deviceIdProvider.GetOrCreateDeviceId(dataRoot);
            return new DeviceIdentityHandler(deviceId, sp.GetRequiredService<ILogger<DeviceIdentityHandler>>());
        });
        services.AddHttpClient("DotNetCloudSync")
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler)
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<DeviceIdentityHandler>();

        services.AddSingleton<ISyncContextManager, SyncContextManager>();

        return services;
    }
}
