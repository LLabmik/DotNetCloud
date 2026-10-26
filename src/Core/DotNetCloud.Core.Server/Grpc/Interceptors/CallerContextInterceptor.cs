using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that constructs and injects a <see cref="CallerContext"/>
/// for an already-authenticated module call.
/// </summary>
/// <remarks>
/// <para>
/// This interceptor only ever runs after <see cref="AuthenticationInterceptor"/> has proven the
/// caller's module identity with the supervisor-issued secret, so anything it accepts is an
/// attestation by an authenticated module rather than an assertion by an arbitrary process.
/// </para>
/// <para>
/// The caller <i>type</i> is derived here and never read from a header: a module that supplies
/// an end-user id acts as <see cref="CallerType.User"/>, and a module acting on its own behalf
/// acts as <see cref="CallerType.Module"/>. <see cref="CallerType.System"/> is unreachable from
/// the module transport — system-level work happens in-process inside the core with an explicit
/// supervisor identity.
/// </para>
/// <para>
/// The end-user identity is not taken on trust either. It arrives as a core-signed assertion in
/// <see cref="ModuleIdentityMetadata.UserAssertionHeader"/>, which only the core can produce; the
/// assertion must verify against the core's public key, be scoped to the calling module (so a
/// Files assertion cannot be replayed by Chat), and be unexpired. Roles are then re-read from the
/// identity store — a module can choose which user it acts for, but never what that user may do.
/// </para>
/// </remarks>
internal sealed class CallerContextInterceptor : Interceptor
{
    private readonly ILogger<CallerContextInterceptor> _logger;
    private readonly ICallerIdentityResolver _identityResolver;
    private readonly ModuleAssertionKeyRing _assertionKeyRing;

    public CallerContextInterceptor(
        ILogger<CallerContextInterceptor> logger,
        ICallerIdentityResolver identityResolver,
        ModuleAssertionKeyRing assertionKeyRing)
    {
        _logger = logger;
        _identityResolver = identityResolver;
        _assertionKeyRing = assertionKeyRing;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await InjectCallerContextAsync(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await InjectCallerContextAsync(context);
        await continuation(request, responseStream, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await InjectCallerContextAsync(context);
        return await continuation(requestStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await InjectCallerContextAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task InjectCallerContextAsync(ServerCallContext context)
    {
        // Direct client calls (desktop/mobile) are authenticated as end users, not as modules.
        if (context.UserState.ContainsKey("ClientUserId"))
        {
            var clientUserIdText = context.UserState["ClientUserId"] as string;

            if (!Guid.TryParse(clientUserIdText, out var clientUserId) || clientUserId == Guid.Empty)
            {
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "The authenticated principal does not identify a user"));
            }

            var clientRoles = await _identityResolver.ResolveRolesAsync(
                clientUserId, context.CancellationToken);

            if (clientRoles is null)
            {
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "The authenticated user is unknown or inactive"));
            }

            context.UserState["CallerContext"] = new CallerContext(
                clientUserId, clientRoles, CallerType.User);
            return;
        }

        if (!context.UserState.TryGetValue("ModuleId", out var moduleIdValue)
            || moduleIdValue is not string moduleId
            || string.IsNullOrEmpty(moduleId))
        {
            // AuthenticationInterceptor runs first and rejects unauthenticated calls, so
            // reaching this point means the interceptor chain was misconfigured.
            _logger.LogError("gRPC call reached CallerContextInterceptor without an authenticated module identity");
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "Unauthenticated module call"));
        }

        var assertionToken = context.RequestHeaders.Get(ModuleIdentityMetadata.UserAssertionHeader)?.Value;

        CallerContext callerContext;

        if (string.IsNullOrEmpty(assertionToken))
        {
            // The module is acting for itself. CallerContext requires a non-empty id even for
            // module callers, so derive a stable pseudo-identity from the verified module id.
            callerContext = new CallerContext(
                ModuleCallerIdentity.ToUserId(moduleId),
                [],
                CallerType.Module);
        }
        else
        {
            // Only a core-signed assertion can name a user, and only for the module it was
            // minted for. A module cannot invent a user, re-scope one, or replay another's.
            if (!_assertionKeyRing.TryValidate(assertionToken, moduleId, out var assertion)
                || assertion is null)
            {
                _logger.LogWarning(
                    "Rejected capability call from module {ModuleId}: invalid, expired or out-of-scope user assertion",
                    moduleId);

                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "Invalid user assertion"));
            }

            var roles = await _identityResolver.ResolveRolesAsync(
                assertion.UserId, context.CancellationToken);

            if (roles is null)
            {
                throw new RpcException(new Status(
                    StatusCode.Unauthenticated,
                    "The impersonated end user is unknown or inactive"));
            }

            context.UserState["UserAssertion"] = assertion;
            callerContext = new CallerContext(assertion.UserId, roles, CallerType.User);
        }

        context.UserState["CallerContext"] = callerContext;

        _logger.LogDebug(
            "Injected CallerContext for module {ModuleId}: UserId={UserId}, Type={CallerType}",
            moduleId, callerContext.UserId, callerContext.Type);
    }
}
