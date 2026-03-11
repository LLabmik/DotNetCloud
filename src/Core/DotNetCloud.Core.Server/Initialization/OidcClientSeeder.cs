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
    /// Ensures first-party public clients (desktop and mobile) are registered for PKCE auth-code flow.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var desktopDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "dotnetcloud-desktop",
            DisplayName = "DotNetCloud Desktop SyncTray",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris =
            {
                new Uri("http://localhost:52701/oauth/callback"),
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

        var mobileDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "dotnetcloud-mobile",
            DisplayName = "DotNetCloud Mobile",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris =
            {
                new Uri("net.dotnetcloud.client://oauth2redirect"),
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
