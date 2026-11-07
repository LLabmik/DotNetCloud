using System.Security.Claims;
using DotNetCloud.Core.Server.Grpc.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the token-introspection claim allow-list: the calling module gets an identity and its
/// roles/scopes, not the token's internal bookkeeping (security stamp, OpenIddict ids).
/// </summary>
[TestClass]
public class TokenIntrospectionClaimExposureTests
{
    [TestMethod]
    [DataRow("sub")]
    [DataRow("name")]
    [DataRow("email")]
    [DataRow("preferred_username")]
    [DataRow("role")]
    [DataRow("roles")]
    [DataRow("scope")]
    [DataRow("client_id")]
    [DataRow("sid")]
    public void IsExposedClaim_IdentityOrAuthorizationClaim_IsForwarded(string claimType)
    {
        Assert.IsTrue(TokenIntrospectionServiceImpl.IsExposedClaim(claimType));
    }

    [TestMethod]
    public void IsExposedClaim_MappedIdentityClaims_AreForwarded()
    {
        // IntrospectionResult.ToPrincipal rebuilds the principal from these types.
        Assert.IsTrue(TokenIntrospectionServiceImpl.IsExposedClaim(ClaimTypes.NameIdentifier));
        Assert.IsTrue(TokenIntrospectionServiceImpl.IsExposedClaim(ClaimTypes.Name));
        Assert.IsTrue(TokenIntrospectionServiceImpl.IsExposedClaim(ClaimTypes.Role));
    }

    [TestMethod]
    [DataRow("AspNet.Identity.SecurityStamp")]
    [DataRow("security_stamp")]
    [DataRow("oi_prst")]
    [DataRow("oi_au_id")]
    [DataRow("oi_tkn_id")]
    [DataRow("oi_clid")]
    [DataRow("AspNet.Identity.UserId")]
    public void IsExposedClaim_InternalBookkeepingClaim_IsDropped(string claimType)
    {
        Assert.IsFalse(TokenIntrospectionServiceImpl.IsExposedClaim(claimType));
    }
}
