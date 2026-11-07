using System.Text;
using System.Text.Json;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DotNetCloud.Core.Server.Consent;

/// <summary>
/// Registers (and keeps in sync) the OAuth2 client that represents an installed module.
/// </summary>
/// <remarks>
/// <para>
/// Every installed module becomes a public OIDC client named <c>dnc-module-&lt;short&gt;</c> with
/// <see cref="ConsentTypes.Explicit"/> consent and PKCE required. There is deliberately no client
/// secret: consent is a browser-to-core flow, and shipping a secret into a module process would
/// turn a consent record into a credential that could mint tokens without a user present.
/// </para>
/// <para>
/// The OpenIddict application also carries the module id as a property, because the consent plane
/// stores grants against the <em>module</em>, not against an OAuth client id, and the authorize
/// endpoint needs to map one to the other.
/// </para>
/// </remarks>
internal sealed class ModuleOidcClientRegistrar
{
    /// <summary>Application property holding the installed module id.</summary>
    internal const string ModuleIdProperty = "dnc_module_id";

    /// <summary>Prefix of every module client id.</summary>
    internal const string ClientIdPrefix = "dnc-module-";

    private const string DefaultRedirectUri = "https://localhost:5443/apps/oauth/callback";

    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ModuleOidcClientRegistrar> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleOidcClientRegistrar"/> class.</summary>
    /// <param name="applicationManager">OpenIddict application store.</param>
    /// <param name="configuration">Configuration (module redirect URI).</param>
    /// <param name="logger">Logger.</param>
    public ModuleOidcClientRegistrar(
        IOpenIddictApplicationManager applicationManager,
        IConfiguration configuration,
        ILogger<ModuleOidcClientRegistrar> logger)
    {
        _applicationManager = applicationManager;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Derives the deterministic client id for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The OAuth2 client id.</returns>
    public static string GetClientId(string moduleId) =>
        ClientIdPrefix + Slug(moduleId);

    /// <summary>Creates or updates the module's OIDC client.</summary>
    /// <param name="moduleId">The installed module id.</param>
    /// <param name="displayName">The module's display name.</param>
    /// <param name="consentScopes">The scopes the manifest declared.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the client is in sync.</returns>
    public async Task EnsureClientAsync(
        string moduleId,
        string displayName,
        IReadOnlyCollection<string> consentScopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(consentScopes);

        var clientId = GetClientId(moduleId);
        var redirectUri = new Uri(_configuration["OidcClients:ModuleRedirectUri"] ?? DefaultRedirectUri);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? moduleId : displayName,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            RedirectUris = { redirectUri },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OpenId,
                Permissions.Prefixes.Scope + ModuleScopes.Profile,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
            Properties =
            {
                [ModuleIdProperty] = JsonSerializer.SerializeToElement(moduleId),
            },
        };

        foreach (var scope in consentScopes.Where(ModuleScopes.IsConsentScope).Distinct(StringComparer.Ordinal))
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        var existing = await _applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existing is null)
        {
            await _applicationManager.CreateAsync(descriptor, cancellationToken);
            _logger.LogInformation("Registered OIDC client {ClientId} for module {ModuleId}", clientId, LogSanitizer.Sanitize(moduleId));
            return;
        }

        await _applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
    }

    /// <summary>Removes the module's OIDC client.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the client is gone.</returns>
    public async Task RemoveClientAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var clientId = GetClientId(moduleId);
        var existing = await _applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            await _applicationManager.DeleteAsync(existing, cancellationToken);
            _logger.LogInformation("Removed OIDC client {ClientId} for module {ModuleId}", clientId, LogSanitizer.Sanitize(moduleId));
        }
    }

    /// <summary>
    /// Shortens a module id into something usable inside a client id: lowercase alphanumerics and
    /// single dashes, at most 64 characters.
    /// </summary>
    /// <param name="moduleId">The module id (for example <c>vendor.receipt-scanner</c>).</param>
    /// <returns>The slug.</returns>
    internal static string Slug(string moduleId)
    {
        var builder = new StringBuilder(moduleId.Length);
        var lastWasDash = false;

        foreach (var character in moduleId)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }

            if (builder.Length >= 64)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
