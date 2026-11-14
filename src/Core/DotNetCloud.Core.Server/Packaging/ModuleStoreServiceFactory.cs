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
/// <para>
/// The admin settings page can override the index URL at runtime
/// (<c>dotnetcloud.core</c>/<see cref="DotNetCloud.Core.Constants.SystemSettingKeys.ModuleStoreIndexUrl"/>).
/// When the host registers a <see cref="ModuleStoreSettingsProvider"/> the returned client
/// re-resolves the source on each operation, so a saved URL takes effect without a restart.
/// </para>
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
        var timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;

        IModuleStoreService Build(ModuleStoreOptions effective)
        {
            if (!string.IsNullOrWhiteSpace(effective.IndexUrl))
            {
                var http = services.GetService<IHttpClientFactory>()
                    ?.CreateClient(RemoteModuleIndexStoreService.HttpClientName)
                    ?? SharedHttpClient;

                return new RemoteModuleIndexStoreService(
                    http,
                    Options.Create(effective),
                    timeProvider,
                    loggerFactory?.CreateLogger<RemoteModuleIndexStoreService>()
                    ?? NullLogger<RemoteModuleIndexStoreService>.Instance);
            }

            return new LocalModuleFeedStoreService(
                Options.Create(effective),
                services.GetRequiredService<IOptions<ProcessSupervisorOptions>>(),
                loggerFactory?.CreateLogger<LocalModuleFeedStoreService>()
                ?? NullLogger<LocalModuleFeedStoreService>.Instance);
        }

        var settings = services.GetService<ModuleStoreSettingsProvider>();

        return settings is null
            ? Build(options.Value)
            : new ConfiguredModuleStoreService(Build, settings, options.Value);
    }
}
