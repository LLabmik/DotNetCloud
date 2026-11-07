using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for the gRPC-backed <see cref="IModuleSettings"/> client.
/// </summary>
public static class ModuleSettingsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IModuleSettings"/> backed by the core's <c>GetSetting</c> /
    /// <c>SetSetting</c> capability RPCs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModuleSettingsClient(this IServiceCollection services)
    {
        services.TryAddSingleton<IModuleSettings, ModuleSettingsGrpcClient>();
        return services;
    }
}
