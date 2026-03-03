using DotNetCloud.Core.Authorization;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that constructs and injects a <see cref="CallerContext"/>
/// from gRPC metadata headers into the call context.
/// </summary>
/// <remarks>
/// Modules include caller context information in gRPC metadata headers.
/// This interceptor extracts that information and makes it available
/// to capability service implementations via <c>context.UserState["CallerContext"]</c>.
/// </remarks>
internal sealed class CallerContextInterceptor : Interceptor
{
    private readonly ILogger<CallerContextInterceptor> _logger;

    public CallerContextInterceptor(ILogger<CallerContextInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        InjectCallerContext(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        InjectCallerContext(context);
        await continuation(request, responseStream, context);
    }

    private void InjectCallerContext(ServerCallContext context)
    {
        var moduleId = context.UserState.TryGetValue("ModuleId", out var mid)
            ? mid as string
            : null;

        var userIdHeader = context.RequestHeaders.Get("caller-user-id")?.Value;
        var callerTypeHeader = context.RequestHeaders.Get("caller-type")?.Value ?? "Module";
        var rolesHeader = context.RequestHeaders.Get("caller-roles")?.Value;

        var userId = Guid.TryParse(userIdHeader, out var uid) ? uid : Guid.Empty;
        var callerType = Enum.TryParse<CallerType>(callerTypeHeader, ignoreCase: true, out var ct)
            ? ct
            : CallerType.Module;

        var roles = string.IsNullOrEmpty(rolesHeader)
            ? Array.Empty<string>()
            : rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var callerContext = new CallerContext(
            userId,
            roles,
            callerType);

        context.UserState["CallerContext"] = callerContext;

        _logger.LogDebug(
            "Injected CallerContext for module {ModuleId}: UserId={UserId}, Type={CallerType}",
            moduleId, userId, callerType);
    }
}
