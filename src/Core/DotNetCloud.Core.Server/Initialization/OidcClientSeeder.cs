using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Seeds required OAuth2/OIDC client applications used by first-party clients.
/// Redirect URIs are configurable via <c>OidcClients:Desktop:RedirectUri</c> and
/// <c>OidcClients:Mobile:RedirectUri</c> settings.
/// </summary>
internal sealed class OidcClientSeeder
{
    private const string DefaultDesktopRedirectUri = "http://localhost:52701/oauth/callback";
    private const string DefaultMobileRedirectUri = "net.dotnetcloud.client://oauth2redirect";

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OidcClientSeeder> _logger;

    public OidcClientSeeder(
        IOpenIddictApplicationManager applicationManager,
        IConfiguration configuration,
        ILogger<OidcClientSeeder> logger)
    {
        _applicationManager = applicationManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Ensures first-party public clients (desktop and mobile) are registered for PKCE auth-code flow.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var desktopRedirectUri = _configuration["OidcClients:Desktop:RedirectUri"] ?? DefaultDesktopRedirectUri;
        var mobileRedirectUri = _configuration["OidcClients:Mobile:RedirectUri"] ?? DefaultMobileRedirectUri;

        var desktopDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "dotnetcloud-desktop",
            DisplayName = "DotNetCloud Desktop SyncTray",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris =
            {
                new Uri(desktopRedirectUri),
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + "files:read",
                Permissions.Prefixes.Scope + "files:write",
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        var mobileDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "dotnetcloud-mobile",
            DisplayName = "DotNetCloud Mobile",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris =
            {
                new Uri(mobileRedirectUri),
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + "files:read",
                Permissions.Prefixes.Scope + "files:write",
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        await UpsertClientAsync(desktopDescriptor);
        await UpsertClientAsync(mobileDescriptor);
    }

    private async Task UpsertClientAsync(OpenIddictApplicationDescriptor descriptor)
    {
        var existing = await _applicationManager.FindByClientIdAsync(descriptor.ClientId!);
        if (existing is null)
        {
            await _applicationManager.CreateAsync(descriptor);
            _logger.LogInformation("Seeded OIDC client '{ClientId}'.", descriptor.ClientId);
            return;
        }

        await _applicationManager.UpdateAsync(existing, descriptor);
        _logger.LogInformation("Updated OIDC client '{ClientId}' permissions/scopes.", descriptor.ClientId);
    }
}
