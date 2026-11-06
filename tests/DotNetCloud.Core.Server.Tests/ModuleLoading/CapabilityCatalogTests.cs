using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Tests for <see cref="CapabilityCatalog"/>.
/// </summary>
[TestClass]
public class CapabilityCatalogTests
{
    [TestMethod]
    [DataRow(nameof(IUserDirectory), CapabilityTier.Public)]
    [DataRow(nameof(ICurrentUserContext), CapabilityTier.Public)]
    [DataRow(nameof(INotificationService), CapabilityTier.Public)]
    [DataRow(nameof(IEventBus), CapabilityTier.Public)]
    [DataRow(nameof(IStorageProvider), CapabilityTier.Restricted)]
    [DataRow(nameof(IModuleSettings), CapabilityTier.Restricted)]
    [DataRow(nameof(ITeamDirectory), CapabilityTier.Restricted)]
    [DataRow(nameof(IUserManager), CapabilityTier.Privileged)]
    [DataRow(nameof(IBackupProvider), CapabilityTier.Privileged)]
    [DataRow(nameof(IAdminSharedFolderCleanup), CapabilityTier.Privileged)]
    public void GetCapabilityTier_KnownCapability_ReturnsExpectedTier(string capability, CapabilityTier expected)
    {
        var tier = CapabilityCatalog.GetCapabilityTier(capability);

        Assert.AreEqual(expected, tier);
    }

    [TestMethod]
    [DataRow(nameof(IAuditLogger))]
    [DataRow(nameof(ISearchProvider))]
    [DataRow(nameof(IRealtimeBroadcaster))]
    public void GetCapabilityTier_ModuleCapabilityRpcBackedCapability_IsRestricted(string capability)
    {
        // Regression: these capabilities back the LogAudit, SubmitSearchIndex and
        // BroadcastRealtimeEvent RPCs. They were absent from the catalog, which left the RPCs
        // unmapped and therefore completely unenforced.
        var tier = CapabilityCatalog.GetCapabilityTier(capability);

        Assert.AreEqual(CapabilityTier.Restricted, tier);
    }

    [TestMethod]
    public void GetCapabilityTier_ForbiddenInterface_ReturnsForbidden()
    {
        var tier = CapabilityCatalog.GetCapabilityTier("CoreDbContext");

        Assert.AreEqual(CapabilityTier.Forbidden, tier);
    }

    [TestMethod]
    public void GetCapabilityTier_UnknownCapability_ReturnsNull()
    {
        var tier = CapabilityCatalog.GetCapabilityTier("ISomethingUndefined");

        Assert.IsNull(tier);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GetCapabilityTier_BlankCapability_ReturnsNull(string? capability)
    {
        var tier = CapabilityCatalog.GetCapabilityTier(capability);

        Assert.IsNull(tier);
    }

    [TestMethod]
    [DataRow(nameof(IStorageProvider), true)]
    [DataRow(nameof(IAdminSharedFolderCleanup), true)]
    [DataRow(nameof(IUserDirectory), false)]
    [DataRow("CoreDbContext", false)]
    [DataRow("ISomethingUndefined", false)]
    public void RequiresGrant_TierDeterminesGrantGovernance(string capability, bool expected)
    {
        Assert.AreEqual(expected, CapabilityCatalog.RequiresGrant(capability));
    }
}
