using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Services;
using Grpc.Core;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Base class for module lifecycle gRPC services that additionally reports the
/// module host's background service run status to the core's admin System page.
/// </summary>
/// <remarks>
/// <para>
/// Hosts that run background services override <see cref="BackgroundServiceTracker"/>
/// with the host's tracker singleton. Hosts without one return an empty list rather
/// than failing the call.
/// </para>
/// <para>
/// <see cref="EraseUserData"/> is implemented here so every module answers the account-erasure
/// contract (F12) the same way. A host that stores per-user data registers an
/// <see cref="IModuleUserErasureHandler"/> with <c>AddModuleUserErasureHandler&lt;T&gt;()</c>;
/// a host that does not answers <c>not_applicable</c> instead of failing, because "I hold nothing"
/// and "I could not erase" must not look the same to the orchestrator.
/// </para>
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

    /// <inheritdoc />
    public override async Task<EraseUserDataResponse> EraseUserData(
        EraseUserDataRequest request,
        ServerCallContext context)
    {
        var moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID");
        var handler = ModuleUserErasureHandlers.Resolve(moduleId);

        if (handler is null)
        {
            // No handler means the module holds no per-user data. Reporting success with
            // not_applicable keeps the orchestrator honest without pretending data was erased.
            return new EraseUserDataResponse { Acknowledged = true, NotApplicable = true };
        }

        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            return new EraseUserDataResponse
            {
                Acknowledged = false,
                Error = "user_id must be a non-empty GUID.",
            };
        }

        _ = Guid.TryParse(request.RequestId, out var requestId);

        var erasureContext = new ModuleUserErasureContext(
            requestId,
            userId,
            request.Scopes.ToArray());

        ModuleUserErasureResult result;
        try
        {
            result = await handler.EraseAsync(erasureContext, context.CancellationToken);
        }
        catch (Exception ex)
        {
            result = ModuleUserErasureResult.Failed(ex.Message);
        }

        var response = new EraseUserDataResponse
        {
            Acknowledged = result.Acknowledged,
            NotApplicable = result.NotApplicable,
            Error = result.Error ?? string.Empty,
        };

        foreach (var (scope, count) in result.DeletedCounts)
        {
            response.DeletedCounts[scope] = count;
        }

        return response;
    }
}
