using System.Security.Claims;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that authenticates module callers against the credential the process
/// supervisor issued when it launched them.
/// </summary>
/// <remarks>
/// A <c>module-id</c> header on its own is only a claim: the core's gRPC listener is a local
/// cleartext HTTP/2 endpoint, so any process able to reach it could assert any module name.
/// The supervisor mints a unique secret per module launch and hands it to the module process
/// out of band; this interceptor requires the matching secret and binds the module identity in
/// <see cref="ServerCallContext.UserState"/> for downstream interceptors and services.
/// </remarks>
internal sealed class AuthenticationInterceptor : Interceptor
{
    /// <summary>
    /// gRPC services that desktop and mobile clients call directly with a bearer token rather than
    /// with a module credential. Keeping this explicit (rather than inferring it from the headers)
    /// means a new service cannot accidentally inherit the client-facing exemption.
    /// </summary>
    private static readonly string[] ClientFacingServicePrefixes =
    [
        "/dotnetcloud.files.FilesService/",
    ];

    private readonly ILogger<AuthenticationInterceptor> _logger;
    private readonly ModuleCredentialStore _credentials;

    public AuthenticationInterceptor(
        ILogger<AuthenticationInterceptor> logger,
        ModuleCredentialStore credentials)
    {
        _logger = logger;
        _credentials = credentials;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        Authenticate(context);
        return await continuation(requestStream, context);
    }

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
        if (IsClientFacing(context.Method))
        {
            AuthenticateClient(context);
            return;
        }

        var moduleId = context.RequestHeaders.Get(ModuleIdentityMetadata.ModuleIdHeader)?.Value;
        var secret = context.RequestHeaders.Get(ModuleIdentityMetadata.ModuleSecretHeader)?.Value;

        if (string.IsNullOrEmpty(moduleId))
        {
            _logger.LogWarning("gRPC call rejected: missing module-id metadata from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing module-id metadata header"));
        }

        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning(
                "gRPC call rejected: missing module-secret metadata from {Peer} (module-id {ModuleId})",
                context.Peer, moduleId);
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Missing module-secret metadata header"));
        }

        if (!_credentials.Validate(moduleId, secret))
        {
            _logger.LogWarning(
                "gRPC call rejected: invalid module credential for {ModuleId} from {Peer}",
                moduleId, context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid module credential"));
        }

        // Store the verified module identity for downstream use. Everything after this point
        // may trust "ModuleId" because it was proven by the supervisor-issued secret.
        context.UserState["ModuleId"] = moduleId;

        _logger.LogDebug("Authenticated gRPC call from module {ModuleId}", moduleId);
    }

    private static bool IsClientFacing(string? method) =>
        method is not null
        && ClientFacingServicePrefixes.Any(prefix =>
            method.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Authenticates a direct client call. There is no module credential on this path — the caller
    /// is an end user, authenticated by the ASP.NET Core authentication middleware that runs inside
    /// the gRPC pipeline before interceptors.
    /// </summary>
    private void AuthenticateClient(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "gRPC client call to {Method} rejected: no authenticated principal from {Peer}",
                context.Method, context.Peer);

            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }

        // Presence of this key marks the call as client-facing for the caller-context interceptor;
        // it is set even when the claim was missing so the failure is reported accurately.
        context.UserState["ClientUserId"] = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? string.Empty;
    }
}
