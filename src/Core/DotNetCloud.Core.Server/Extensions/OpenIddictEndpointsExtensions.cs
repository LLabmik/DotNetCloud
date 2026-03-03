using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
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
        app.MapPost("/connect/authorize", (Delegate)HandleAuthorizeEndpoint);
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

        // Extract the flow type
        var grantType = request.GrantType;

        if (grantType == GrantTypes.AuthorizationCode)
        {
            // Authorization Code flow - handled by ASP.NET Core and OpenIddict
            return Results.Ok(new { message = "Authorization Code flow - token will be issued by OpenIddict" });
        }
        else if (grantType == GrantTypes.RefreshToken)
        {
            // Refresh Token flow - handled by ASP.NET Core and OpenIddict
            return Results.Ok(new { message = "Refresh Token flow - new token will be issued by OpenIddict" });
        }
        else if (grantType == GrantTypes.ClientCredentials)
        {
            // Client Credentials flow - for service-to-service authentication
            return Results.Ok(new { message = "Client Credentials flow - token will be issued by OpenIddict" });
        }
        else
        {
            // Unsupported grant type
            return Results.BadRequest(new { error = "unsupported_grant_type", error_description = $"The '{grantType}' grant type is not supported." });
        }
    }

    private static async Task<IResult> HandleAuthorizeEndpoint(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // For authorization code flow, redirect to login if not authenticated
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return Results.Redirect($"/login?returnUrl={Uri.EscapeDataString(context.Request.GetEncodedPathAndQuery())}");
        }

        // After login, OpenIddict handles the consent screen and code generation
        return Results.Ok(new { message = "Authorization endpoint - redirect to consent page or issue code" });
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
