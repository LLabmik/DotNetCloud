using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
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

    private static Task<IResult> HandleAuthorizeEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<IResult>(Results.Challenge(
                authenticationSchemes: [IdentityConstants.ApplicationScheme],
                properties: new AuthenticationProperties
                {
                    RedirectUri = context.Request.GetEncodedPathAndQuery()
                }));
        }

        var identity = new ClaimsIdentity(
            context.User.Claims,
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        if (!identity.HasClaim(static c => c.Type == Claims.Subject))
        {
            var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue(Claims.Subject);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                identity.SetClaim(Claims.Subject, subject);
            }
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetDestinations(static claim => claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role or Claims.Subject
                => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        });

        return Task.FromResult<IResult>(Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme));
    }

    private static async Task<IResult> HandleLogoutEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Sign out the user
        await context.SignOutAsync();

        // Redirect to the post-logout redirect URI if provided
        var postLogoutRedirectUri = request.PostLogoutRedirectUri;
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            return Results.Redirect(postLogoutRedirectUri);
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
        // Verify the access token
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return Results.Unauthorized();
        }

        // Return user information
        return Results.Ok(new
        {
            sub = context.User.FindFirst("sub")?.Value,
            email = context.User.FindFirst("email")?.Value,
            email_verified = bool.TryParse(context.User.FindFirst("email_verified")?.Value, out var verified) && verified,
            name = context.User.FindFirst("name")?.Value,
            preferred_username = context.User.FindFirst("preferred_username")?.Value
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
