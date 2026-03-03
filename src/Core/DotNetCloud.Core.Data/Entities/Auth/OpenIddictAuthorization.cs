using OpenIddict.EntityFrameworkCore.Models;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents a user consent/authorization granted to an OAuth2/OIDC client application.
/// </summary>
/// <remarks>
/// Inherits all standard authorization properties from
/// <see cref="OpenIddictEntityFrameworkCoreAuthorization{TKey, TApplication, TToken}"/>,
/// including subject (user), scopes, status, creation date, and type.
///
/// <para>
/// OpenIddict's EF Core store manages persistence. Table naming overrides are applied in
/// <c>CoreDbContext.ConfigureAuthenticationModels</c>.
/// </para>
/// </remarks>
public class OpenIddictAuthorization
    : OpenIddictEntityFrameworkCoreAuthorization<Guid, OpenIddictApplication, OpenIddictToken>
{
}
