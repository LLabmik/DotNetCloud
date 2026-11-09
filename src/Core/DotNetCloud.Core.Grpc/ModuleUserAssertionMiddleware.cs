using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// ASP.NET Core middleware for module hosts that adopts the core-signed user assertion supplied on
/// an inbound proxied request.
/// </summary>
/// <remarks>
/// <para>
/// The core mints an assertion when it proxies a request for an authenticated user, so the module
/// does not have to trust its own read of the forwarded cookie or bearer token. A request that
/// arrives with an assertion that fails verification is rejected outright rather than quietly
/// downgraded — a tampered or out-of-scope assertion is an attempt to name somebody else, and
/// continuing as "anonymous" would hide it.
/// </para>
/// <para>
/// Requests without an assertion are allowed to continue as the module itself: health probes,
/// internal callbacks and scheduled work legitimately have no end user.
/// </para>
/// <para>
/// A verified assertion also becomes the request's <see cref="HttpContext.User"/> principal. That is
/// what lets a module's own authorization helpers (<c>[Authorize]</c>, user-id comparisons in gRPC
/// handlers) work for a call that originated in the core — the core calling a module on a user's
/// behalf over the peer channel — without the module ever accepting a caller-supplied user id. Only
/// the core can sign an assertion, so this is strictly stronger evidence than a forwarded cookie.
/// </para>
/// </remarks>
public sealed class ModuleUserAssertionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _audience;
    private readonly System.Security.Cryptography.ECDsa? _verificationKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleUserAssertionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public ModuleUserAssertionMiddleware(RequestDelegate next)
    {
        _next = next;
        _audience = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? string.Empty;

        // Imported once: key import costs far more than the verification itself, and this
        // middleware runs on every proxied request.
        _verificationKey = ModuleUserAssertions.TryImportPublicKey(
            Environment.GetEnvironmentVariable(
                ModuleIdentityMetadata.AssertionPublicKeyEnvironmentVariable));
    }

    /// <summary>
    /// Verifies the inbound assertion, publishes it to the flow, and continues the pipeline.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers[ModuleIdentityMetadata.UserAssertionHeader].ToString();

        if (string.IsNullOrEmpty(token))
        {
            await _next(context);
            return;
        }

        var audience = _audience;

        if (_verificationKey is null
            || !ModuleUserAssertions.TryVerify(
                token,
                _verificationKey,
                audience,
                DateTimeOffset.UtcNow,
                out var assertion)
            || assertion is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items[ModuleIdentityMetadata.UserAssertionHeader] = assertion;

        // Adopt the asserted identity as the request principal. The assertion was verified against
        // the core's public key and is audience-bound to this module, so an authenticated principal
        // here is core-attested, not caller-supplied.
        context.User = CreatePrincipal(assertion);

        using var scope = ModuleUserAssertionContext.Set(token);
        await _next(context);
    }

    /// <summary>
    /// Builds the request principal a verified assertion stands for.
    /// </summary>
    /// <param name="assertion">The verified assertion.</param>
    /// <returns>A principal whose name identifier is the asserted user.</returns>
    private static System.Security.Claims.ClaimsPrincipal CreatePrincipal(ModuleUserAssertion assertion)
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    assertion.UserId.ToString()),
            ],
            authenticationType: "DncModuleUserAssertion");

        return new System.Security.Claims.ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Registration helpers for <see cref="ModuleUserAssertionMiddleware"/>.
/// </summary>
public static class ModuleUserAssertionMiddlewareExtensions
{
    /// <summary>
    /// Adds the module user-assertion middleware. Call it immediately after the application is
    /// built so the assertion is available to everything that follows.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseModuleUserAssertion(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ModuleUserAssertionMiddleware>();
    }
}
