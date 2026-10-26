using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// DI registration for the gRPC-backed <see cref="IPlatformEventPublisher"/>.
/// </summary>
/// <remarks>
/// Module hosts call this in <c>Program.cs</c> next to <c>AddAuditLogger()</c>. It is what lets a
/// module report a platform event (calendar reminder, share expiry) without delivering the
/// notification itself — the core owns delivery.
/// </remarks>
public static class PlatformEventPublisherServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPlatformEventPublisher"/> backed by the <c>CoreCapabilities.PublishEvent</c>
    /// gRPC capability. Call this in each module host's startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPlatformEventPublisher(this IServiceCollection services)
    {
        services.TryAddSingleton<IPlatformEventPublisher, PlatformEventPublisher>();
        return services;
    }
}
