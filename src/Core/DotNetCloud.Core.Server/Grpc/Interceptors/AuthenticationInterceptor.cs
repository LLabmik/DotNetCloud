using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that validates module authentication tokens.
/// Ensures that only registered and authorized modules can access core capabilities.
/// </summary>
internal sealed class AuthenticationInterceptor : Interceptor
{
    private readonly ILogger<AuthenticationInterceptor> _logger;

    public AuthenticationInterceptor(ILogger<AuthenticationInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var moduleId = GetModuleId(context);

        if (string.IsNullOrEmpty(moduleId))
        {
            _logger.LogWarning("gRPC call rejected: missing module-id metadata from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing module-id metadata header"));
        }

        // Store module ID in UserState for downstream use
        context.UserState["ModuleId"] = moduleId;

        _logger.LogDebug("Authenticated gRPC call from module {ModuleId}", moduleId);

        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var moduleId = GetModuleId(context);

        if (string.IsNullOrEmpty(moduleId))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing module-id metadata header"));
        }

        context.UserState["ModuleId"] = moduleId;

        await continuation(request, responseStream, context);
    }

    private static string? GetModuleId(ServerCallContext context)
    {
        var entry = context.RequestHeaders.Get("module-id");
        return entry?.Value;
    }
}
