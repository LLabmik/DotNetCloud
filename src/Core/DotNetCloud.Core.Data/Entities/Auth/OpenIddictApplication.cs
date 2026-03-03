using OpenIddict.EntityFrameworkCore.Models;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC client application registered in the DotNetCloud authorization server.
/// </summary>
/// <remarks>
/// Inherits all standard OAuth2/OIDC application properties from
/// <see cref="OpenIddictEntityFrameworkCoreApplication{TKey, TAuthorization, TToken}"/>,
/// including client credentials, redirect URIs, scopes, consent type, and requirements.
///
/// <para>
/// OpenIddict's EF Core store manages persistence. The <c>UseOpenIddict&lt;...&gt;()</c> call
/// in <c>CoreDbContext.OnModelCreating</c> applies the built-in entity configuration.
/// </para>
/// </remarks>
public class OpenIddictApplication
    : OpenIddictEntityFrameworkCoreApplication<Guid, OpenIddictAuthorization, OpenIddictToken>
{
}
