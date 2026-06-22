using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// ASP.NET Core authentication handler that validates bearer tokens by calling
/// Core.Server's TokenIntrospection gRPC service. Implements the OAuth2
/// introspection pattern: resource servers validate tokens by asking the
/// authorization server, rather than validating signatures locally.
/// </summary>
public sealed class IntrospectionAuthenticationHandler
    : AuthenticationHandler<IntrospectionAuthenticationOptions>
{
    private readonly ITokenIntrospectionClient _introspectionClient;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public IntrospectionAuthenticationHandler(
        IOptionsMonitor<IntrospectionAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenIntrospectionClient introspectionClient,
        IMemoryCache cache)
        : base(options, logger, encoder)
    {
        _introspectionClient = introspectionClient;
        _cache = cache;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract the bearer token from the Authorization header.
        var authorization = Request.Headers.Authorization;
        if (StringValues.IsNullOrEmpty(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        var header = authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Bearer token is empty.");
        }

        // Check cache first.
        var cacheKey = ComputeCacheKey(token);
        if (_cache.TryGetValue<AuthenticateResult>(cacheKey, out var cachedResult))
        {
            Logger.LogDebug("Introspection auth: cache HIT for token {Hash}", cacheKey);
            return cachedResult!;
        }

        Logger.LogDebug("Introspection auth: cache MISS for token {Hash}", cacheKey);

        // Call the introspection service.
        try
        {
            var moduleId = Options.ModuleId
                ?? Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID")
                ?? "unknown";

            var requiredAudience = Options.RequiredAudience ?? moduleId;

            var result = await _introspectionClient.IntrospectAsync(
                token, moduleId, requiredAudience, Context.RequestAborted);

            if (!result.Active)
            {
                var failResult = AuthenticateResult.Fail(
                    result.ErrorDescription ?? "Token is not active.");

                // Cache failures briefly to avoid thundering introspection calls
                // when a bad token is retried.
                CacheResult(cacheKey, failResult, TimeSpan.FromSeconds(30));

                return failResult;
            }

            var principal = result.ToPrincipal(Scheme.Name);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            var successResult = AuthenticateResult.Success(ticket);

            // Cache successful results. TTL is capped to prevent stale tokens
            // from being accepted indefinitely if a token is revoked.
            var cacheDuration = Options.CacheDuration;
            CacheResult(cacheKey, successResult, cacheDuration);

            Logger.LogDebug(
                "Introspection auth: token valid for sub={Sub}, cached for {Duration}",
                result.Sub, cacheDuration);

            return successResult;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Introspection auth: introspection call failed");

            // Don't cache transport errors — they're likely transient.
            return AuthenticateResult.Fail($"Introspection service error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate =
            "Bearer error=\"invalid_token\", error_description=\"The token is invalid or expired.\"";
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }

    private void CacheResult(string cacheKey, AuthenticateResult result, TimeSpan duration)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration,
            Size = 1,
        };
        _cache.Set(cacheKey, result, options);
    }

    /// <summary>
    /// Computes a cache key from the raw token using SHA-256.
    /// Using the hash prevents the raw token from being stored in
    /// the in-process cache in cleartext.
    /// </summary>
    private static string ComputeCacheKey(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return "introspect:" + Convert.ToHexStringLower(hash);
    }
}
