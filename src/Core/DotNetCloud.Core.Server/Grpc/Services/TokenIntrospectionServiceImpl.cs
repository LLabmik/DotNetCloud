using DotNetCloud.Core;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Grpc.TokenIntrospection;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;
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
/// The scope set is narrowed twice before it leaves this service: to the scopes the module
/// declared in its manifest, and — for the scopes that cover personal data — to what the token's
/// user has consented to for this module. An introspection response is therefore never a wider
/// grant than the user approved.
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
    private readonly IModuleManifestRegistry _manifests;
    private readonly ModuleConsentPolicy _consentPolicy;
    private readonly TokenValidationParameters _validationParameters;

    public TokenIntrospectionServiceImpl(
        ILogger<TokenIntrospectionServiceImpl> logger,
        IModuleManifestRegistry manifests,
        ModuleConsentPolicy consentPolicy)
    {
        _logger = logger;
        _manifests = manifests;
        _consentPolicy = consentPolicy;

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
    public override async Task<IntrospectTokenResponse> IntrospectToken(
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

            return new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Module identity mismatch. Caller module ID does not match authenticated gRPC identity."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Token is empty."
            };
        }

        try
        {
            // Validate the JWT using the same signing keys OpenIddict uses.
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                request.Token, _validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return new IntrospectTokenResponse
                {
                    Active = false,
                    ErrorDescription = "Token is not a valid JWT."
                };
            }

            // Audience check. The proto documents required_audience as mandatory when supplied;
            // it was previously ignored, so a module could validate a token that was never
            // intended for it. Only enforced when the caller asks for it, because OpenIddict
            // currently issues client-scoped audiences.
            if (!SatisfiesAudience(jwtToken.Audiences, request.RequiredAudience))
            {
                _logger.LogWarning(
                    "IntrospectToken: token audience does not include the required audience for module {Module}",
                    LogSanitizer.Sanitize(request.CallerModuleId));

                return new IntrospectTokenResponse
                {
                    Active = false,
                    ErrorDescription = "Token audience mismatch."
                };
            }

            // Extract claims and scopes.
            var scopes = principal.FindFirst("scope")?.Value
                ?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                ?? Array.Empty<string>();

            var subscriber = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? string.Empty;

            var effectiveScopes = await FilterScopesAsync(
                authenticatedModuleId!,
                subscriber,
                scopes,
                context.CancellationToken);

            var maySeeEmail = await MaySeeEmailAsync(
                authenticatedModuleId!,
                subscriber,
                context.CancellationToken);

            var response = new IntrospectTokenResponse
            {
                Active = true,
                Sub = subscriber,
                Username = principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst("name")?.Value
                    ?? string.Empty,
                Email = maySeeEmail
                    ? principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? principal.FindFirst("email")?.Value
                        ?? string.Empty
                    : string.Empty,
            };

            response.Scopes.AddRange(effectiveScopes);

            foreach (var claim in principal.Claims)
            {
                if (!ExposedClaimTypes.Contains(claim.Type))
                {
                    continue;
                }

                if (!maySeeEmail && IsEmailClaim(claim.Type))
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
                LogSanitizer.Sanitize(subscriber), LogSanitizer.Sanitize(request.CallerModuleId));

            return response;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogDebug(ex, "IntrospectToken: token expired");
            return new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Token has expired."
            };
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "IntrospectToken: token validation failed");
            return new IntrospectTokenResponse
            {
                Active = false,
                ErrorDescription = "Token validation failed."
            };
        }
    }

    /// <summary>
    /// Narrows a token's scopes to the calling module's declared scopes and, for personal-data
    /// scopes, to what the token's user has consented to.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="subscriber">The token subject, when it names a user.</param>
    /// <param name="tokenScopes">The scopes carried by the token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scopes the module may actually use.</returns>
    private async Task<IReadOnlyList<string>> FilterScopesAsync(
        string moduleId,
        string subscriber,
        IReadOnlyList<string> tokenScopes,
        CancellationToken cancellationToken)
    {
        var declared = _manifests.GetUserConsentScopes(moduleId);

        // A module that declares no consent scopes predates the consent plane; the token's own
        // scope set remains the answer so existing clients keep working.
        if (declared is null || declared.Count == 0)
        {
            return tokenScopes;
        }

        HashSet<string>? consented = null;
        if (Guid.TryParse(subscriber, out var userId) && userId != Guid.Empty)
        {
            var granted = await _consentPolicy.GetGrantedScopesAsync(moduleId, userId, cancellationToken);
            consented = new HashSet<string>(granted, StringComparer.Ordinal);
        }

        return FilterScopes(declared, consented, tokenScopes);
    }

    /// <summary>
    /// Narrows a token's scopes to what the calling module may use, given the scopes it declared
    /// and (for personal-data scopes) what the user consented to.
    /// </summary>
    /// <param name="declared">The scopes the module declared, or <see langword="null"/> when unknown.</param>
    /// <param name="consented">The scopes the user consented to, or <see langword="null"/> when there is no user subject.</param>
    /// <param name="tokenScopes">The scopes carried by the token.</param>
    /// <returns>The scopes the module may actually use.</returns>
    /// <remarks>
    /// Only the consent catalog is filtered. A standard OIDC scope (<c>openid</c>,
    /// <c>offline_access</c>) is part of the authentication contract, not a per-module grant, and an
    /// unrecognized scope was never a consent decision. <c>email</c> is the exception: it is only
    /// meaningful next to <c>profile</c> consent.
    /// </remarks>
    internal static IReadOnlyList<string> FilterScopes(
        IReadOnlyCollection<string>? declared,
        IReadOnlySet<string>? consented,
        IReadOnlyList<string> tokenScopes)
    {
        if (declared is null || declared.Count == 0)
        {
            return tokenScopes;
        }

        var declaredSet = declared as IReadOnlySet<string>
            ?? new HashSet<string>(declared, StringComparer.Ordinal);
        var maySeeEmail = consented is not null && consented.Contains(ModuleScopes.Profile);

        return tokenScopes
            .Where(scope =>
            {
                if (ModuleScopes.IsConsentScope(scope))
                {
                    return declaredSet.Contains(scope)
                        && consented is not null
                        && consented.Contains(scope);
                }

                if (string.Equals(scope, "email", StringComparison.Ordinal))
                {
                    return declaredSet.Contains(ModuleScopes.Profile) && maySeeEmail;
                }

                return true;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Determines whether the calling module may see the token user's email address.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="subscriber">The token subject.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the user consented to <c>profile</c> for this module.</returns>
    private async Task<bool> MaySeeEmailAsync(
        string moduleId,
        string subscriber,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subscriber, out var userId) || userId == Guid.Empty)
        {
            return false;
        }

        return await _consentPolicy.HasConsentAsync(moduleId, userId, ModuleScopes.Profile, cancellationToken);
    }

    private static bool IsEmailClaim(string claimType) =>
        string.Equals(claimType, "email", StringComparison.Ordinal)
        || string.Equals(claimType, ClaimTypes.Email, StringComparison.Ordinal);

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
