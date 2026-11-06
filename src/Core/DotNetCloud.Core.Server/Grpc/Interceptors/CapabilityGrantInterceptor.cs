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
/// The map below is <strong>exhaustive</strong> for the <c>CoreCapabilities</c> service in
/// <c>module_capabilities.proto</c>. Every RPC must map to a capability or be added to an explicit
/// allow-list with a comment explaining why it is not capability-governed — an unmapped RPC is
/// enforced by nothing, which is how <c>LogAudit</c>, <c>SubmitSearchIndex</c> and
/// <c>GetGroupsForUser</c> previously escaped enforcement.
/// </para>
/// </remarks>
internal sealed class CapabilityGrantInterceptor : Interceptor
{
    /// <summary>
    /// Maps a fully-qualified gRPC method name to the capability it exercises.
    /// </summary>
    /// <remarks>
    /// Group/team lookups all enforce <see cref="ITeamDirectory"/>: that is the directory
    /// capability modules declare, and it is the only grant the module hosts actually hold when
    /// they resolve shared-folder or team membership. Unifying <c>IGroupDirectory</c> and
    /// <c>ITeamDirectory</c> behind one catalog entry is part of the Module API v1 work (phase A2).
    /// </remarks>
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
        ["GetGroupsForUser"] = nameof(ITeamDirectory),
        ["GetTeam"] = nameof(ITeamDirectory),
        ["GetTeamsForUser"] = nameof(ITeamDirectory),
        ["CleanupAdminSharedFolder"] = nameof(IAdminSharedFolderCleanup),

        // Re-tiered away from Public INotificationService: broadcasting a platform-branded
        // real-time event to any group is not the same power as sending a notification.
        ["BroadcastRealtimeEvent"] = nameof(IRealtimeBroadcaster),
        ["LogAudit"] = nameof(IAuditLogger),
        ["SubmitSearchIndex"] = nameof(ISearchProvider),
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

        var tier = CapabilityCatalog.GetCapabilityTier(capability);
        if (tier is CapabilityTier.Public)
        {
            return;
        }

        // Unknown tiers fail closed: there is no grant row for a capability that is not in the
        // catalog, so a mapping added without a matching catalog entry is denied loudly instead
        // of silently allowed.

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

    /// <summary>
    /// Resolves the capability a gRPC method exercises.
    /// </summary>
    /// <param name="method">The fully-qualified gRPC method name.</param>
    /// <returns>The capability name, or <see langword="null"/> when the method is not mapped.</returns>
    internal static string? ResolveCapability(string method)
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
