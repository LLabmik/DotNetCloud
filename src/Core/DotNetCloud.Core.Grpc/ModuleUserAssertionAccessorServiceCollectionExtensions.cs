using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for <see cref="IModuleUserAssertionAccessor"/>.
/// </summary>
/// <remarks>
/// Module hosts register this when a core-initiated operation needs to know which user the core is
/// acting for. Hosts that only consume assertions by forwarding them to the core (the default
/// behaviour of <see cref="ModuleUserAssertionContext"/>) do not need it.
/// </remarks>
public static class ModuleUserAssertionAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IModuleUserAssertionAccessor"/> backed by the core's published
    /// verification key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModuleUserAssertionAccessor(this IServiceCollection services)
    {
        services.TryAddSingleton<IModuleUserAssertionAccessor, ModuleUserAssertionAccessor>();
        return services;
    }
}
