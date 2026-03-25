using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using DotNetCloud.Core.Data.Entities.Identity;
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
            identity.SetClaim(Claims.Email, user.Email);
        }

        // Copy role claims from the authenticated principal
        foreach (var roleClaim in context.User.FindAll(ClaimTypes.Role))
        {
            identity.AddClaim(Claims.Role, roleClaim.Value);
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

    private static async Task<IResult> HandleLogoutEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Sign out the user
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
                    email_verified = user.EmailConfirmed,
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
