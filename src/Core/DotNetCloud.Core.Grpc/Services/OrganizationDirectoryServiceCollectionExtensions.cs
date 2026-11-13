using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for a gRPC-backed <see cref="IOrganizationDirectory"/> capability client.
/// </summary>
/// <remarks>
/// <para>
/// Module hosts call this in <c>Program.cs</c> (next to <c>AddGrpcTeamDirectory()</c>) so their
/// services and UI endpoints can resolve the acting user's organizations without any access to
/// <c>core.organizations</c>.
/// </para>
/// <para>
/// The client connects to Core.Server via <c>DOTNETCLOUD_CORE_ENDPOINT</c> and degrades gracefully
/// (empty listing) when the endpoint is absent, so a unit-test or standalone host still starts.
/// Listing is deliberately not cached: organization membership is an authorization input, and a
/// stale cache would keep a removed member inside a module for the length of the TTL.
/// </para>
/// </remarks>
public static class OrganizationDirectoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOrganizationDirectory"/> backed by the <c>CoreCapabilities</c>
    /// gRPC service. Call this in each module host's startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGrpcOrganizationDirectory(this IServiceCollection services)
    {
        services.TryAddSingleton<GrpcOrganizationDirectory>();
        services.TryAddSingleton<IOrganizationDirectory>(
            sp => sp.GetRequiredService<GrpcOrganizationDirectory>());
        return services;
    }
}
