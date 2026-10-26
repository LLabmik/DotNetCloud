using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// gRPC server interceptor for module hosts that rejects data-plane calls which do not carry the
/// credential the process supervisor issued to this module.
/// </summary>
/// <remarks>
/// <para>
/// Module host gRPC services act on the user id supplied in the request payload, so an anonymous
/// listener let any local process read or write another user's data by simply naming them. This
/// interceptor closes that: the core presents the module's own supervisor-issued secret on every
/// call, and anything else is rejected before a request reaches a service implementation.
/// </para>
/// <para>
/// The secret is read from <see cref="ModuleIdentityMetadata.ModuleSecretEnvironmentVariable"/>,
/// which only the supervisor sets. A host that was started outside the supervisor has no secret
/// and therefore fails closed — that is intentional; a bypass flag would defeat the control.
/// </para>
/// </remarks>
public sealed class ModulePeerAuthenticationInterceptor : Interceptor
{
    private readonly byte[]? _expectedSecret;
    private readonly ILogger<ModulePeerAuthenticationInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulePeerAuthenticationInterceptor"/> class,
    /// reading the module credential from the environment.
    /// </summary>
    /// <param name="logger">Logger for rejected calls.</param>
    public ModulePeerAuthenticationInterceptor(ILogger<ModulePeerAuthenticationInterceptor> logger)
    {
        _logger = logger;

        var secret = Environment.GetEnvironmentVariable(
            ModuleIdentityMetadata.ModuleSecretEnvironmentVariable);

        _expectedSecret = string.IsNullOrEmpty(secret)
            ? null
            : Encoding.UTF8.GetBytes(secret);
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(request, context);
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        await continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        await continuation(requestStream, responseStream, context);
    }

    private void Authenticate(ServerCallContext context)
    {
        if (_expectedSecret is null)
        {
            _logger.LogError(
                "Rejecting gRPC call to {Method}: {Variable} is not set, so this host was not " +
                "launched by the process supervisor and cannot authenticate its peer",
                context.Method, ModuleIdentityMetadata.ModuleSecretEnvironmentVariable);

            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Module host is missing its supervisor-issued credential"));
        }

        var presented = context.RequestHeaders.Get(ModuleIdentityMetadata.ModuleSecretHeader)?.Value;

        if (string.IsNullOrEmpty(presented))
        {
            _logger.LogWarning(
                "Rejecting unauthenticated gRPC call to {Method} from {Peer}",
                context.Method, context.Peer);

            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Missing module-secret metadata header"));
        }

        var actual = Encoding.UTF8.GetBytes(presented);
        if (actual.Length != _expectedSecret.Length
            || !CryptographicOperations.FixedTimeEquals(actual, _expectedSecret))
        {
            _logger.LogWarning(
                "Rejecting gRPC call to {Method} with an invalid module credential from {Peer}",
                context.Method, context.Peer);

            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Invalid module credential"));
        }
    }
}
