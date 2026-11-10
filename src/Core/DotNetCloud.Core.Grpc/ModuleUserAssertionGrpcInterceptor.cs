using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// Adopts the core-signed user assertion carried on an inbound gRPC call as the ambient user for
/// the duration of the handler.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ModuleUserAssertionMiddleware"/> does this for HTTP requests; a core-initiated gRPC
/// call (media import, scheduled work) needs the same treatment, otherwise a module handler can only
/// see a user id in the request payload, which any local caller could invent.
/// </para>
/// <para>
/// A call with no assertion continues as the module itself — a health probe or a peer callback
/// legitimately has no end user. A call that carries an assertion which fails verification is
/// rejected with <c>UNAUTHENTICATED</c> rather than silently downgraded, because a tampered or
/// out-of-audience assertion is an attempt to name somebody else.
/// </para>
/// </remarks>
public sealed class ModuleUserAssertionGrpcInterceptor : Interceptor
{
    private readonly string _audience;
    private readonly System.Security.Cryptography.ECDsa? _verificationKey;

    /// <summary>Initializes a new instance of the <see cref="ModuleUserAssertionGrpcInterceptor"/> class.</summary>
    public ModuleUserAssertionGrpcInterceptor()
    {
        _audience = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? string.Empty;

        // Imported once: key import costs far more than a verification, and this runs per call.
        _verificationKey = ModuleUserAssertions.TryImportPublicKey(
            Environment.GetEnvironmentVariable(
                ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable));
    }

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        var token = context.RequestHeaders.GetValue(ModuleIdentityMetadata.UserAssertionHeader);

        if (string.IsNullOrEmpty(token))
        {
            return continuation(request, context);
        }

        if (_verificationKey is null
            || !ModuleUserAssertions.TryVerify(token, _verificationKey, _audience, DateTimeOffset.UtcNow, out _))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "The user assertion could not be verified for this module."));
        }

        return InvokeWithAssertion(token, request, context, continuation);
    }

    private static async Task<TResponse> InvokeWithAssertion<TRequest, TResponse>(
        string token,
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        using var scope = ModuleUserAssertionContext.Set(token);
        return await continuation(request, context);
    }
}
