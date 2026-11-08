using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Chooses the store client implementation from configuration.
/// </summary>
/// <remarks>
/// One decision, one source: when <see cref="ModuleStoreOptions.IndexUrl"/> is set the remote
/// signed index is used, otherwise the local feed directory. Both implement
/// <see cref="IModuleStoreService"/>, so the installer, the CLI and the admin API never know which
/// one is active — and a server cannot end up silently installing from a feed nobody configured.
/// </remarks>
public static class ModuleStoreServiceFactory
{
    /// <summary>
    /// A client for hosts that do not register <see cref="IHttpClientFactory"/> (the CLI). Timeouts
    /// are applied per request by the store client itself.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Creates the configured store client.</summary>
    /// <param name="services">The service provider being built.</param>
    /// <returns>The store client.</returns>
    public static IModuleStoreService Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetRequiredService<IOptions<ModuleStoreOptions>>();
        var loggerFactory = services.GetService<ILoggerFactory>();

        if (!string.IsNullOrWhiteSpace(options.Value.IndexUrl))
        {
            var http = services.GetService<IHttpClientFactory>()
                ?.CreateClient(RemoteModuleIndexStoreService.HttpClientName)
                ?? SharedHttpClient;

            return new RemoteModuleIndexStoreService(
                http,
                options,
                services.GetService<TimeProvider>() ?? TimeProvider.System,
                loggerFactory?.CreateLogger<RemoteModuleIndexStoreService>()
                ?? NullLogger<RemoteModuleIndexStoreService>.Instance);
        }

        return new LocalModuleFeedStoreService(
            options,
            services.GetRequiredService<IOptions<ProcessSupervisorOptions>>(),
            loggerFactory?.CreateLogger<LocalModuleFeedStoreService>()
            ?? NullLogger<LocalModuleFeedStoreService>.Instance);
    }
}
