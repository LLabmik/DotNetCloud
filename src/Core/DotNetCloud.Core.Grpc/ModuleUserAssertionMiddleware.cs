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

        using var scope = ModuleUserAssertionContext.Set(token);
        await _next(context);
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
