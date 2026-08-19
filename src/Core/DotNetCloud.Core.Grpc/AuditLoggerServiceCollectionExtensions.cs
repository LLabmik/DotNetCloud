using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for the gRPC-backed <see cref="IAuditLogger"/> client.
/// </summary>
/// <remarks>
/// Module hosts call this in <c>Program.cs</c> next to <c>AddTokenIntrospection()</c>
/// so module services can record audit entries that Core.Server persists (SOC 2 CC4).
/// The client connects to Core.Server via <c>DOTNETCLOUD_CORE_ENDPOINT</c>.
/// </remarks>
public static class AuditLoggerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAuditLogger"/> backed by the <c>CoreCapabilities.LogAudit</c>
    /// gRPC capability. Call this in each module host's startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuditLogger(this IServiceCollection services)
    {
        services.TryAddSingleton<IAuditLogger, AuditLoggerGrpcClient>();
        return services;
    }
}
