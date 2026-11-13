using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Consent;
using DotNetCloud.Core.Server.ModuleLoading;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for mapping OpenIddict endpoints and configuring the authorization flow.
/// </summary>
public static class OpenIddictEndpointsExtensions
{
    /// <summary>
    /// Map OpenIddict protocol endpoints to the application.
    /// </summary>
    /// <param name="app">The web application builder</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication MapOpenIddictEndpoints(this WebApplication app)
    {
        app.MapPost("/connect/token", (Delegate)HandleTokenEndpoint);
        app.MapMethods("/connect/authorize", ["GET", "POST"], (Delegate)HandleAuthorizeEndpoint);
        app.MapPost("/connect/logout", (Delegate)HandleLogoutEndpoint);
        app.MapPost("/connect/revoke", (Delegate)HandleRevokeEndpoint);
        app.MapGet("/connect/userinfo", (Delegate)HandleUserInfoEndpoint);
        app.MapPost("/connect/introspect", (Delegate)HandleIntrospectEndpoint);

        return app;
    }

    private static async Task<IResult> HandleTokenEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authenticationResult = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (authenticationResult.Succeeded && authenticationResult.Principal is not null)
            {
                return Results.SignIn(
                    authenticationResult.Principal,
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The authorization grant is no longer valid.",
                }));
        }

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId ?? string.Empty);
            identity.SetClaim(Claims.Name, request.ClientId ?? string.Empty);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());
            principal.SetDestinations(static _ => [Destinations.AccessToken]);

            return Results.SignIn(
                principal,
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Results.BadRequest(new
        {
            error = OpenIddictConstants.Errors.UnsupportedGrantType,
            error_description = $"The '{request.GrantType}' grant type is not supported."
        });
    }

    private static async Task<IResult> HandleAuthorizeEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return Results.Challenge(
                authenticationSchemes: [IdentityConstants.ApplicationScheme],
                properties: new AuthenticationProperties
                {
                    RedirectUri = context.Request.GetEncodedPathAndQuery()
                });
        }

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Resolve subject (user GUID) from the authenticated principal
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(Claims.Subject);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            identity.SetClaim(Claims.Subject, subject);
        }

        // Look up the user to populate OIDC-standard claims from the database
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = subject is not null ? await userManager.FindByIdAsync(subject) : null;

        if (user is not null)
        {
            identity.SetClaim(Claims.Name, user.DisplayName);
            identity.SetClaim(Claims.PreferredUsername, user.UserName);

            // Email is optional; only emit the claim when the user actually has one.
            if (!string.IsNullOrEmpty(user.Email))
            {
                identity.SetClaim(Claims.Email, user.Email);
            }
        }

        // Copy role claims from the authenticated principal
        foreach (var roleClaim in context.User.FindAll(ClaimTypes.Role))
        {
            identity.AddClaim(Claims.Role, roleClaim.Value);
        }

        // Consent plane (phase A3). Runs before the sign-in response so that a prompt, or a user's
        // refusal, can replace it. First-party clients are Implicit (D3) and never reach this point.
        var consentResult = await HandleConsentAsync(context, request, subject);
        if (consentResult is not null)
        {
            return consentResult;
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        // Route claims to appropriate token destinations
        principal.SetDestinations(static claim => claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role or Claims.Subject
                or Claims.PreferredUsername
                => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });

        return Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Applies the per-user consent plane to an authorization request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="request">The OpenIddict authorization request.</param>
    /// <param name="subject">The authenticated user's id.</param>
    /// <returns>
    /// A result that replaces the sign-in (a prompt redirect or an <c>access_denied</c> error), or
    /// <see langword="null"/> when the request may proceed.
    /// </returns>
    /// <remarks>
    /// Only clients registered with <see cref="ConsentTypes.Explicit"/> <em>and</em> carrying a
    /// module id are subject to the consent plane. The three first-party clients are Implicit (D3),
    /// so their sign-in is unchanged and no prompt appears. A non-module explicit client is outside
    /// this plane entirely: there is no module to store a grant against.
    /// </remarks>
    private static async Task<IResult?> HandleConsentAsync(
        HttpContext context,
        OpenIddictRequest request,
        string? subject)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || !Guid.TryParse(subject, out var userId))
        {
            return null;
        }

        var store = context.RequestServices.GetRequiredService<ConsentRequestStore>();

        // A recorded decision coming back from the consent page. The id is single-use and bound to
        // the signed-in user, so it cannot be replayed or redirected to someone else.
        if (context.Request.Query.TryGetValue(ConsentEndpoints.DecisionParameter, out var decisionId))
        {
            var pending = store.Consume(decisionId.ToString(), userId);
            if (pending is null)
            {
                return Results.Content(
                    ConsentPageRenderer.RenderError(
                        "This consent decision is unknown, has expired, or was already used. " +
                        "Return to the application and start again."),
                    "text/html; charset=utf-8",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (pending.Decision is ConsentDecision.Denied)
            {
                return Results.Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.AccessDenied,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The user denied the requested permissions.",
                    }));
            }

            // Approved: the consent rows were written by the POST handler; complete the request.
            return null;
        }

        var applicationManager = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applicationManager.FindByClientIdAsync(request.ClientId, context.RequestAborted)
            ?? await TryProvisionModuleClientAsync(context, request.ClientId);

        if (application is null)
        {
            return null;
        }

        var consentType = await applicationManager.GetConsentTypeAsync(application, context.RequestAborted);
        if (!string.Equals(consentType, ConsentTypes.Explicit, StringComparison.Ordinal))
        {
            return null;
        }

        var properties = await applicationManager.GetPropertiesAsync(application, context.RequestAborted);
        var moduleId = properties.TryGetValue(ModuleOidcClientRegistrar.ModuleIdProperty, out var moduleIdValue)
            ? moduleIdValue.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var consentScopes = request.GetScopes()
            .Where(scope => !string.IsNullOrWhiteSpace(scope) && ModuleScopes.IsConsentScope(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (consentScopes.Length == 0)
        {
            return null;
        }

        var consent = context.RequestServices.GetRequiredService<IModuleConsentService>();
        var missing = await consent.GetMissingScopesAsync(moduleId, userId, consentScopes, context.RequestAborted);

        if (missing.Count == 0)
        {
            return null;
        }

        var displayName = await applicationManager.GetDisplayNameAsync(application, context.RequestAborted)
            ?? moduleId;

        var consentRequestId = store.Create(
            userId,
            request.ClientId,
            moduleId,
            displayName,
            missing,
            context.Request.QueryString.Value ?? string.Empty);

        return Results.Redirect($"{ConsentEndpoints.Path}?rid={Uri.EscapeDataString(consentRequestId)}");
    }

    /// <summary>
    /// Provisions the OIDC client for a consent-aware module the reconcile pass has not covered yet.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="clientId">The client id from the authorization request.</param>
    /// <returns>The application, or <see langword="null"/> when the id is not a known module.</returns>
    private static async Task<object?> TryProvisionModuleClientAsync(HttpContext context, string clientId)
    {
        if (!clientId.StartsWith(ModuleOidcClientRegistrar.ClientIdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var manifests = context.RequestServices.GetRequiredService<IModuleManifestRegistry>();

        var moduleId = manifests.GetModuleIds()
            .FirstOrDefault(candidate => string.Equals(
                ModuleOidcClientRegistrar.GetClientId(candidate),
                clientId,
                StringComparison.Ordinal));

        var manifest = moduleId is null ? null : manifests.GetManifest(moduleId);
        if (moduleId is null || manifest is null || manifest.UserConsentScopes.Count == 0)
        {
            return null;
        }

        var registrar = context.RequestServices.GetRequiredService<ModuleOidcClientRegistrar>();
        await registrar.EnsureClientAsync(
            moduleId,
            manifest.Name,
            manifest.UserConsentScopes,
            context.RequestAborted);

        var applicationManager = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        return await applicationManager.FindByClientIdAsync(clientId, context.RequestAborted);
    }

    private static async Task<IResult> HandleLogoutEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        await context.SignOutAsync();

        // Validate post-logout redirect URI to prevent open redirect attacks.
        // Only allow local (relative) URIs — external redirects are blocked.
        var postLogoutRedirectUri = request.PostLogoutRedirectUri;
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            // Only allow local paths (starts with /, does not start with // which is protocol-relative)
            if (postLogoutRedirectUri.StartsWith("/", StringComparison.Ordinal)
                && !postLogoutRedirectUri.StartsWith("//", StringComparison.Ordinal))
            {
                return Results.LocalRedirect(postLogoutRedirectUri);
            }

            // Log and reject external redirect attempts
            var logger = context.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger("OpenIddict.Logout");
            logger?.LogWarning(
                "Blocked open redirect in post-logout URI: {RedirectUri}",
                postLogoutRedirectUri);
        }

        return Results.Ok(new { message = "Logout successful" });
    }

    private static async Task<IResult> HandleRevokeEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Token revocation - handled by OpenIddict
        return Results.Ok(new { message = "Token revocation - token will be revoked by OpenIddict" });
    }

    private static async Task<IResult> HandleUserInfoEndpoint(HttpContext context)
    {
        // Verify the access token (OpenIddict validates the bearer token via passthrough)
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return Results.Unauthorized();
        }

        var sub = context.User.FindFirstValue(Claims.Subject)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Look up user from database for authoritative claim values
        if (sub is not null)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(sub);

            if (user is not null)
            {
                return Results.Ok(new
                {
                    sub,
                    email = user.Email,
                    email_verified = user.EmailConfirmed && !string.IsNullOrEmpty(user.Email),
                    name = user.DisplayName,
                    preferred_username = user.UserName
                });
            }
        }

        // Fallback: return whatever claims exist on the token
        return Results.Ok(new
        {
            sub,
            email = context.User.FindFirstValue(Claims.Email),
            email_verified = bool.TryParse(context.User.FindFirstValue("email_verified"), out var verified) && verified,
            name = context.User.FindFirstValue(Claims.Name),
            preferred_username = context.User.FindFirstValue(Claims.PreferredUsername)
        });
    }

    private static async Task<IResult> HandleIntrospectEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Token introspection - handled by OpenIddict
        return Results.Ok(new { message = "Token introspection - metadata will be returned by OpenIddict" });
    }
}
