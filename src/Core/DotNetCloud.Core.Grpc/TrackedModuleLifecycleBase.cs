using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Services;
using Grpc.Core;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Base class for module lifecycle gRPC services that additionally reports the
/// module host's background service run status to the core's admin System page.
/// </summary>
/// <remarks>
/// Hosts that run background services override <see cref="BackgroundServiceTracker"/>
/// with the host's tracker singleton. Hosts without one return an empty list rather
/// than failing the call.
/// </remarks>
public abstract class TrackedModuleLifecycleBase : ModuleLifecycle.ModuleLifecycleBase
{
    /// <summary>
    /// Gets the background service tracker for this host, or <c>null</c> when the host
    /// does not track background services.
    /// </summary>
    protected virtual IBackgroundServiceTracker? BackgroundServiceTracker => null;

    /// <inheritdoc />
    public override Task<GetBackgroundServicesResponse> GetBackgroundServices(
        GetBackgroundServicesRequest request,
        ServerCallContext context) =>
        Task.FromResult(BackgroundServiceTracker?.ToResponse() ?? new GetBackgroundServicesResponse());
}
