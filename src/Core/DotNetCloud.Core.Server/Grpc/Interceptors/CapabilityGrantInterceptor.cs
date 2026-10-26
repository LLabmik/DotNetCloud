using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Server.ModuleLoading;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that enforces <c>ModuleCapabilityGrants</c> for the capabilities that require
/// an explicit grant.
/// </summary>
/// <remarks>
/// <para>
/// Public-tier capabilities (user directory, notifications, event bus) are available to every
/// installed module. Restricted and privileged capabilities must have a persisted grant row for
/// the calling module; the row is created when the module is registered from its manifest and can
/// be revoked from the admin surface, which takes effect on the very next call.
/// </para>
/// <para>
/// RPCs that do not map to a modelled capability are allowed through. The capability model does
/// not yet cover every internal bridge (audit, search indexing), and inventing tier assignments
/// for them here would silently deny working traffic.
/// </para>
/// </remarks>
internal sealed class CapabilityGrantInterceptor : Interceptor
{
    /// <summary>
    /// Maps a fully-qualified gRPC method name to the capability it exercises.
    /// </summary>
    private static readonly Dictionary<string, string> MethodCapabilities = new(StringComparer.Ordinal)
    {
        ["GetUser"] = nameof(IUserDirectory),
        ["SearchUsers"] = nameof(IUserDirectory),
        ["GetCurrentUser"] = nameof(ICurrentUserContext),
        ["SendNotification"] = nameof(INotificationService),
        ["PublishEvent"] = nameof(IEventBus),
        ["GetSetting"] = nameof(IModuleSettings),
        ["SetSetting"] = nameof(IModuleSettings),
        ["GetGroup"] = nameof(ITeamDirectory),
        ["GetTeam"] = nameof(ITeamDirectory),
        ["GetTeamsForUser"] = nameof(ITeamDirectory),
        ["CleanupAdminSharedFolder"] = nameof(IAdminSharedFolderCleanup),
        ["BroadcastRealtimeEvent"] = nameof(INotificationService),
    };

    private readonly ILogger<CapabilityGrantInterceptor> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public CapabilityGrantInterceptor(
        ILogger<CapabilityGrantInterceptor> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task EnforceAsync(ServerCallContext context)
    {
        var capability = ResolveCapability(context.Method);
        if (capability is null)
        {
            return;
        }

        var tier = CapabilityValidator.GetCapabilityTier(capability);
        if (tier is CapabilityTier.Public)
        {
            return;
        }

        var moduleId = context.UserState.TryGetValue("ModuleId", out var value) ? value as string : null;
        if (string.IsNullOrEmpty(moduleId))
        {
            // AuthenticationInterceptor already rejected this call; defensive only.
            throw Denied("unknown", capability);
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var granted = await dbContext.ModuleCapabilityGrants
            .AnyAsync(g => g.ModuleId == moduleId && g.CapabilityName == capability,
                context.CancellationToken);

        if (granted)
        {
            return;
        }

        _logger.LogWarning(
            "Module {ModuleId} called {Method} without the {Capability} grant",
            moduleId, context.Method, capability);

        throw Denied(moduleId, capability);
    }

    private static RpcException Denied(string moduleId, string capability) =>
        new(new Status(
                StatusCode.PermissionDenied,
                $"[CAPABILITY_NOT_GRANTED] Capability '{capability}' is not granted to module '{moduleId}'."),
            new Metadata { { "error-code", ErrorCodes.CapabilityNotGranted } });

    private static string? ResolveCapability(string method)
    {
        if (string.IsNullOrEmpty(method))
        {
            return null;
        }

        var separator = method.LastIndexOf('/');
        var simpleName = separator >= 0 ? method[(separator + 1)..] : method;

        return MethodCapabilities.TryGetValue(simpleName, out var capability) ? capability : null;
    }
}
