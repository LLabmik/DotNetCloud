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
internal sealed class TokenIntrospectionServiceImpl : TokenIntrospection.TokenIntrospectionBase
{
    private readonly ILogger<TokenIntrospectionServiceImpl> _logger;
    private readonly TokenValidationParameters _validationParameters;

    public TokenIntrospectionServiceImpl(
        ILogger<TokenIntrospectionServiceImpl> logger)
    {
        _logger = logger;

        // Load signing keys from the shared oidc-keys directory.
        // On Core.Server this is always available — it's the same directory
        // OpenIddict uses for signing.
        var dataRoot = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        var oidcKeysDir = Path.Combine(
            !string.IsNullOrWhiteSpace(dataRoot) ? dataRoot : AppContext.BaseDirectory,
            "oidc-keys");

        var signingKeys = OidcKeyManager.LoadAllKeys(
            oidcKeysDir, OidcKeyManager.SigningKeyPrefix, logger);

        _logger.LogInformation(
            "TokenIntrospectionService: loaded {Count} signing key(s) from {Dir}",
            signingKeys.Count, oidcKeysDir);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => signingKeys,
        };
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
                authenticatedModuleId, request.CallerModuleId);

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

            // Audience check intentionally omitted.
            // OpenIddict sets the audience to the issuer URL or client ID,
            // not module-specific identifiers like "dotnetcloud.files".
            // JWT-level audience validation is already disabled (ValidateAudience=false),
            // and scope-based authorization provides sufficient access control.
            // See: CLIENT_SERVER_MEDIATION_HANDOFF.md 20260622 (token introspection architecture)

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
                response.Claims.Add(new ClaimEntry
                {
                    Type = claim.Type,
                    Value = claim.Value,
                });
            }

            _logger.LogDebug(
                "IntrospectToken: token valid for user {Sub}, module {Module}",
                response.Sub, request.CallerModuleId);

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
