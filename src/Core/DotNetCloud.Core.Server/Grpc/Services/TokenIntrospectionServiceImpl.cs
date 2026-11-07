using DotNetCloud.Core;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Grpc.TokenIntrospection;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DotNetCloud.Core.Server.Grpc.Services;

/// <summary>
/// gRPC implementation of the TokenIntrospection service.
/// Validates bearer tokens by verifying their signature against the shared
/// OpenIddict signing keys, then returns scoped claims to the calling module.
/// </summary>
/// <remarks>
/// <para>
/// The caller is a process-isolated module, not the resource owner. Everything returned here is
/// handed to code that must never see more than it needs, so the claim list is an allow-list of
/// identity/authorization claims: OpenIddict internals and ASP.NET Identity bookkeeping (security
/// stamp, token/authorization ids, persisted-grant markers) are dropped rather than forwarded
/// (F15).
/// </para>
/// <para>
/// Scope filtering to the module's declared scopes ships with the consent plane (phase A3), where
/// a module-scope catalog exists to filter against.
/// </para>
/// </remarks>
internal sealed class TokenIntrospectionServiceImpl : TokenIntrospection.TokenIntrospectionBase
{
    /// <summary>
    /// Claims forwarded to the calling module. Anything else is dropped: the module needs an
    /// identity and its roles/scopes, not the token's internal bookkeeping.
    /// </summary>
    private static readonly HashSet<string> ExposedClaimTypes = new(StringComparer.Ordinal)
    {
        "sub",
        "name",
        "preferred_username",
        "email",
        "given_name",
        "family_name",
        "locale",
        "role",
        "roles",
        "scope",
        "scp",
        "client_id",
        "azp",
        "sid",
        "jti",
        "iat",
        "nbf",
        "exp",
        "iss",
        "aud",
        ClaimTypes.NameIdentifier,
        ClaimTypes.Name,
        ClaimTypes.Email,
        ClaimTypes.Role,
    };

    private readonly ILogger<TokenIntrospectionServiceImpl> _logger;
    private readonly TokenValidationParameters _validationParameters;

    public TokenIntrospectionServiceImpl(
        ILogger<TokenIntrospectionServiceImpl> logger)
    {
        _logger = logger;

        // Load signing and encryption keys from the shared oidc-keys directory.
        // On Core.Server this is always available — it's the same directory
        // OpenIddict uses for signing and encryption.
        var dataRoot = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        var oidcKeysDir = Path.Combine(
            !string.IsNullOrWhiteSpace(dataRoot) ? dataRoot : AppContext.BaseDirectory,
            "oidc-keys");

        var signingKeys = OidcKeyManager.LoadAllKeys(
            oidcKeysDir, OidcKeyManager.SigningKeyPrefix, logger);

        var encryptionKeys = OidcKeyManager.LoadAllKeys(
            oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, logger);

        _logger.LogInformation(
            "TokenIntrospectionService: loaded {SigningCount} signing key(s) and {EncryptionCount} encryption key(s) from {Dir}",
            signingKeys.Count, encryptionKeys.Count, oidcKeysDir);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => signingKeys,
            TokenDecryptionKeyResolver = (token, securityToken, kid, parameters) =>
                encryptionKeys.Count > 0 ? encryptionKeys : null,
        };
    }

    /// <summary>
    /// Determines whether a claim type is forwarded to the calling module.
    /// </summary>
    /// <param name="claimType">The raw token claim type.</param>
    /// <returns><see langword="true"/> when the claim is on the exposure allow-list.</returns>
    internal static bool IsExposedClaim(string claimType) => ExposedClaimTypes.Contains(claimType);

    /// <summary>
    /// Determines whether a token satisfies the audience the calling module requires.
    /// </summary>
    /// <param name="tokenAudiences">The token's <c>aud</c> values.</param>
    /// <param name="requiredAudience">
    /// The audience the module requires, or <see langword="null"/>/empty when the module does not
    /// pin one (the default, because OpenIddict issues client-scoped audiences rather than
    /// module-scoped ones until the per-module OIDC clients land in phase A3).
    /// </param>
    /// <returns><see langword="true"/> when no audience is required or one value matches.</returns>
    internal static bool SatisfiesAudience(IEnumerable<string>? tokenAudiences, string? requiredAudience)
    {
        if (string.IsNullOrWhiteSpace(requiredAudience))
        {
            return true;
        }

        return tokenAudiences is not null
            && tokenAudiences.Any(audience =>
                string.Equals(audience, requiredAudience, StringComparison.Ordinal));
    }

    /// <summary>
    /// Validates a bearer token and returns scoped claims for the calling module.
    /// </summary>
    public override Task<IntrospectTokenResponse> IntrospectToken(
        IntrospectTokenRequest request,
        ServerCallContext context)
    {
        // Verify caller module identity matches the request claim.
        // Rejects if the auth interceptor didn't set a module ID (defense in depth).
        var authenticatedModuleId = GetAuthenticatedModuleId(context);
        if (!ValidateCallerModuleId(authenticatedModuleId, request.CallerModuleId))
        {
            _logger.LogWarning(
                "IntrospectToken: module ID mismatch. Auth={Auth}, Request={Req}",
                LogSanitizer.Sanitize(authenticatedModuleId ?? "unknown"),
                LogSanitizer.Sanitize(request.CallerModuleId));

            return Task.FromResult(new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Module identity mismatch. Caller module ID does not match authenticated gRPC identity."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Task.FromResult(new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Token is empty."
            });
        }

        try
        {
            // Validate the JWT using the same signing keys OpenIddict uses.
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                request.Token, _validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return Task.FromResult(new IntrospectTokenResponse
                {
                    Active = false,
                    ErrorDescription = "Token is not a valid JWT."
                });
            }

            // Audience check. The proto documents required_audience as mandatory when supplied;
            // it was previously ignored, so a module could validate a token that was never
            // intended for it. Only enforced when the caller asks for it, because OpenIddict
            // currently issues client-scoped audiences (the per-module clients arrive with A3).
            if (!SatisfiesAudience(jwtToken.Audiences, request.RequiredAudience))
            {
                _logger.LogWarning(
                    "IntrospectToken: token audience does not include the required audience for module {Module}",
                    LogSanitizer.Sanitize(request.CallerModuleId));

                return Task.FromResult(new IntrospectTokenResponse
                {
                    Active = false,
                    ErrorDescription = "Token audience mismatch."
                });
            }

            // Extract claims and scopes.
            var scopes = principal.FindFirst("scope")?.Value
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();

            var response = new IntrospectTokenResponse
            {
                Active = true,
                Sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst("sub")?.Value
                    ?? string.Empty,
                Username = principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst("name")?.Value
                    ?? string.Empty,
                Email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst("email")?.Value
                    ?? string.Empty,
            };

            response.Scopes.AddRange(scopes);

            foreach (var claim in principal.Claims)
            {
                if (!ExposedClaimTypes.Contains(claim.Type))
                {
                    continue;
                }

                response.Claims.Add(new ClaimEntry
                {
                    Type = claim.Type,
                    Value = claim.Value,
                });
            }

            _logger.LogDebug(
                "IntrospectToken: token valid for user {Sub}, module {Module}",
                response.Sub, LogSanitizer.Sanitize(request.CallerModuleId));

            return Task.FromResult(response);
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogDebug(ex, "IntrospectToken: token expired");
            return Task.FromResult(new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Token has expired."
            });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "IntrospectToken: token validation failed");
            return Task.FromResult(new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = $"Token validation failed: {ex.Message}"
            });
        }
    }

    /// Extracts the authenticated module ID from the gRPC call context.
    /// Set by the AuthenticationInterceptor during call handling.
    /// Returns null only if the interceptor was bypassed — which itself
    /// would be blocked by the interceptor on all registered gRPC services.
    private static string? GetAuthenticatedModuleId(ServerCallContext context)
    {
        if (context.UserState.TryGetValue("ModuleId", out var value) && value is string moduleId)
            return moduleId;
        return null;
    }

    /// <summary>
    /// Validates that the introspection request carries a module ID that matches
    /// the authenticated gRPC caller. If the interceptor didn't set a module ID
    /// (should never happen in normal operation), the request is always rejected.
    /// </summary>
    private static bool ValidateCallerModuleId(string? authenticatedModuleId, string requestModuleId)
    {
        if (string.IsNullOrEmpty(authenticatedModuleId))
            return false; // interceptor bypassed — reject all

        return string.Equals(authenticatedModuleId, requestModuleId, StringComparison.Ordinal);
    }
}
