using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// DI registration for introspection services.
/// </summary>
public static class IntrospectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the token introspection client and related services.
    /// Call this in the module host's startup.
    /// </summary>
    public static IServiceCollection AddTokenIntrospection(this IServiceCollection services)
    {
        services.TryAddSingleton<ITokenIntrospectionClient, TokenIntrospectionClient>();
        services.AddMemoryCache(); // for token validation cache

        return services;
    }
}
