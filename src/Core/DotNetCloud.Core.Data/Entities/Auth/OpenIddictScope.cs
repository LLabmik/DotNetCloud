using OpenIddict.EntityFrameworkCore.Models;

namespace DotNetCloud.Core.Data.Entities.Auth;

/// <summary>
/// Represents an OAuth2/OIDC scope definition available on the authorization server.
/// </summary>
/// <remarks>
/// Inherits all standard scope properties from
/// <see cref="OpenIddictEntityFrameworkCoreScope{TKey}"/>,
/// including name, display name, description, resources, and localized variants.
///
/// <para>
/// OpenIddict's EF Core store manages persistence. Table naming overrides are applied in
/// <c>CoreDbContext.ConfigureAuthenticationModels</c>.
/// </para>
/// </remarks>
public class OpenIddictScope : OpenIddictEntityFrameworkCoreScope<Guid>
{
}
