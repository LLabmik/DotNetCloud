using DotNetCloud.Core.Server.Grpc.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the token-introspection audience rule: when a module asks for a specific audience the
/// token must carry it, and when it asks for none the previous behaviour is unchanged.
/// </summary>
[TestClass]
public class TokenIntrospectionAudienceTests
{
    [TestMethod]
    public void SatisfiesAudience_NoRequirement_IsSatisfied()
    {
        Assert.IsTrue(TokenIntrospectionServiceImpl.SatisfiesAudience(null, null));
        Assert.IsTrue(TokenIntrospectionServiceImpl.SatisfiesAudience(["anything"], null));
        Assert.IsTrue(TokenIntrospectionServiceImpl.SatisfiesAudience(["anything"], "  "));
    }

    [TestMethod]
    public void SatisfiesAudience_MatchingAudience_IsSatisfied()
    {
        Assert.IsTrue(TokenIntrospectionServiceImpl.SatisfiesAudience(
            ["https://cloud.example", "dnc-module-dotnetcloud.files"],
            "dnc-module-dotnetcloud.files"));
    }

    [TestMethod]
    public void SatisfiesAudience_MissingAudience_IsRejected()
    {
        Assert.IsFalse(TokenIntrospectionServiceImpl.SatisfiesAudience(
            ["https://cloud.example"],
            "dnc-module-dotnetcloud.files"));
    }

    [TestMethod]
    public void SatisfiesAudience_NoAudiencesOnToken_IsRejected()
    {
        Assert.IsFalse(TokenIntrospectionServiceImpl.SatisfiesAudience([], "dnc-module-dotnetcloud.files"));
        Assert.IsFalse(TokenIntrospectionServiceImpl.SatisfiesAudience(null, "dnc-module-dotnetcloud.files"));
    }

    [TestMethod]
    public void SatisfiesAudience_ComparisonIsOrdinal()
    {
        // Audience values are opaque identifiers; a case-insensitive match would let a look-alike
        // audience through.
        Assert.IsFalse(TokenIntrospectionServiceImpl.SatisfiesAudience(
            ["DNC-MODULE-DOTNETCLOUD.FILES"],
            "dnc-module-dotnetcloud.files"));
    }
}
