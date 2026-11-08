using DotNetCloud.Core.Modules.Store;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for the catalogue trust-tier vocabulary (phase B5).
/// </summary>
[TestClass]
public class ModuleStoreTrustTierTests
{
    [TestMethod]
    [DataRow("official", ModuleStoreTrustTier.Official)]
    [DataRow("verified-publisher", ModuleStoreTrustTier.VerifiedPublisher)]
    [DataRow("community", ModuleStoreTrustTier.Community)]
    [DataRow("unverified", ModuleStoreTrustTier.Unverified)]
    [DataRow("  OFFICIAL  ", ModuleStoreTrustTier.Official)]
    [DataRow("gold", ModuleStoreTrustTier.Unknown)]
    [DataRow("", ModuleStoreTrustTier.Unknown)]
    [DataRow(null, ModuleStoreTrustTier.Unknown)]
    public void Parse_CatalogueValue_ReturnsExpectedTier(string? value, ModuleStoreTrustTier expected)
    {
        Assert.AreEqual(expected, ModuleStoreTrustTiers.Parse(value));
    }

    [TestMethod]
    [DataRow(ModuleStoreTrustTier.Official, "official")]
    [DataRow(ModuleStoreTrustTier.VerifiedPublisher, "verified-publisher")]
    [DataRow(ModuleStoreTrustTier.Community, "community")]
    [DataRow(ModuleStoreTrustTier.Unverified, "unverified")]
    [DataRow(ModuleStoreTrustTier.Unknown, "unknown")]
    public void ToWireValue_EveryTier_ReturnsTheCatalogueValue(
        ModuleStoreTrustTier tier, string expected)
    {
        Assert.AreEqual(expected, tier.ToWireValue());
        Assert.AreEqual(tier, ModuleStoreTrustTiers.Parse(expected), "the value must round-trip");
    }

    [TestMethod]
    public void ToDisplayName_EveryTier_ReturnsANonEmptyLabel()
    {
        foreach (var tier in Enum.GetValues<ModuleStoreTrustTier>())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(tier.ToDisplayName()), tier.ToString());
        }
    }
}
