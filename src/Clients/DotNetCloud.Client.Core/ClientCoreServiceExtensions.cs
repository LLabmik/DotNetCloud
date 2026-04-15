using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.Conflict;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Client.Core;

/// <summary>
/// Registers all DotNetCloud.Client.Core services in the DI container.
/// </summary>
public static class ClientCoreServiceExtensions
{
    /// <summary>
    /// Adds Client.Core services (API client, auth, sync engine, transfer, conflict resolver,
    /// local state DB, and selective sync config) to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tokenStoreDirectory">Directory for secure token storage.</param>
    public static IServiceCollection AddDotNetCloudClientCore(
        this IServiceCollection services,
        string tokenStoreDirectory)
    {
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<DotNetCloudApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            })
            .AddHttpMessageHandler<CorrelationIdHandler>();
        services.AddTransient<IDotNetCloudApiClient, DotNetCloudApiClient>();

        services.AddHttpClient<IOAuth2Service, OAuth2Service>()
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler);

        services.AddSingleton<ITokenStore>(sp =>
            new EncryptedFileTokenStore(
                tokenStoreDirectory,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EncryptedFileTokenStore>>()));

        services.AddTransient<IChunkedTransferClient, ChunkedTransferClient>();
        services.AddTransient<IConflictResolver, ConflictResolver>();
        services.AddTransient<ILocalStateDb, LocalStateDb>();
        services.AddSingleton<ISelectiveSyncConfig, SelectiveSyncConfig>();
        services.AddTransient<ISyncIgnoreParser, SyncIgnoreParser>();

        // Register platform-appropriate locked file reader.
        // VssLockedFileReader uses Windows VSS (requires SYSTEM/Backup Operators privileges).
        // NoOpLockedFileReader is a no-op stub for Linux/macOS.
        if (OperatingSystem.IsWindows())
            services.AddTransient<ILockedFileReader, VssLockedFileReader>();
        else
            services.AddTransient<ILockedFileReader, NoOpLockedFileReader>();

        services.AddTransient<ISyncEngine, SyncEngine>();

        // Update checking service (public endpoint — no auth required).
        services.AddHttpClient<IClientUpdateService, ClientUpdateService>();

        return services;
    }
}
