using OpenIddict.EntityFrameworkCore.Models;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC token (access token, refresh token, authorization code, etc.).
/// </summary>
/// <remarks>
/// Inherits all standard token properties from
/// <see cref="OpenIddictEntityFrameworkCoreToken{TKey, TApplication, TAuthorization}"/>,
/// including type, status, subject, payload, reference ID, and expiration date.
///
/// <para>
/// OpenIddict's EF Core store manages persistence and token lifecycle.
/// Table naming overrides are applied in <c>CoreDbContext.ConfigureAuthenticationModels</c>.
/// </para>
/// </remarks>
public class OpenIddictToken
    : OpenIddictEntityFrameworkCoreToken<Guid, OpenIddictApplication, OpenIddictAuthorization>
{
}
