using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DotNetCloud.Core.Server.Initialization;

/// <summary>
/// Seeds required OAuth2/OIDC client applications used by first-party clients.
/// </summary>
internal sealed class OidcClientSeeder
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ILogger<OidcClientSeeder> _logger;

    public OidcClientSeeder(
        IOpenIddictApplicationManager applicationManager,
        ILogger<OidcClientSeeder> logger)
    {
        _applicationManager = applicationManager;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the desktop SyncTray public client is registered for PKCE auth-code flow.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        const string desktopClientId = "dotnetcloud-desktop";
        const string redirectUri = "http://localhost:52701/oauth/callback";

        var existing = await _applicationManager.FindByClientIdAsync(desktopClientId);
        if (existing is not null)
        {
            _logger.LogDebug("OIDC client '{ClientId}' already exists.", desktopClientId);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = desktopClientId,
            DisplayName = "DotNetCloud Desktop SyncTray",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris =
            {
                new Uri(redirectUri),
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
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

        await _applicationManager.CreateAsync(descriptor);
        _logger.LogInformation("Seeded OIDC desktop client '{ClientId}'.", desktopClientId);
    }
}
