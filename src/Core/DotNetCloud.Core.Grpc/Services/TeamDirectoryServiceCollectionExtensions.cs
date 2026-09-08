using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for a gRPC-backed <see cref="ITeamDirectory"/> capability
/// client, decorated with a short-lived membership cache.
/// </summary>
/// <remarks>
/// <para>
/// Module hosts call this in <c>Program.cs</c> (next to <c>AddTokenIntrospection()</c> /
/// <c>AddAuditLogger()</c>) so their share/read services can resolve a caller's team
/// membership by calling Core.Server's <c>CoreCapabilities.GetTeamsForUser</c> /
/// <c>GetTeam</c> gRPC capabilities.
/// </para>
/// <para>
/// The client connects to Core.Server via <c>DOTNETCLOUD_CORE_ENDPOINT</c> and
/// degrades gracefully (empty membership) when the endpoint is absent. Lookups are
/// cached for a short TTL (default 30 s) to avoid a core round trip per access query.
/// </para>
/// </remarks>
public static class TeamDirectoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITeamDirectory"/> backed by the <c>CoreCapabilities</c>
    /// gRPC service, wrapped in a short-lived cache. Call this in each module host's
    /// startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cacheDuration">Membership cache duration (default 30 seconds).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGrpcTeamDirectory(
        this IServiceCollection services,
        TimeSpan? cacheDuration = null)
    {
        services.TryAddSingleton<GrpcTeamDirectory>();
        services.TryAddSingleton<ITeamDirectory>(sp =>
            new CachedTeamDirectory(
                sp.GetRequiredService<GrpcTeamDirectory>(),
                cacheDuration,
                sp.GetService<ILogger<CachedTeamDirectory>>(),
                sp.GetService<TimeProvider>()));
        return services;
    }
}
