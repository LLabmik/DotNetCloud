using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Core.Tests.Capabilities;

/// <summary>
/// Tests for <see cref="CapabilityCatalog"/>: the tier model and the RPC → capability mapping that
/// the access evaluator and the SDK both read.
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
    [DataRow(nameof(CapabilityTier.Public), false)]
    [DataRow(nameof(CapabilityTier.Restricted), true)]
    [DataRow(nameof(CapabilityTier.Privileged), true)]
    [DataRow(nameof(CapabilityTier.Forbidden), false)]
    public void RequiresGrant_Tier_ReturnsExpected(string tierName, bool expected)
    {
        var capability = Enum.Parse<CapabilityTier>(tierName) switch
        {
            CapabilityTier.Public => nameof(IUserDirectory),
            CapabilityTier.Restricted => nameof(IStorageProvider),
            CapabilityTier.Privileged => nameof(IUserManager),
            _ => "CoreDbContext",
        };

        Assert.AreEqual(expected, CapabilityCatalog.RequiresGrant(capability));
    }

    [TestMethod]
    public void Rpc_BuildsNormalizedPath()
    {
        Assert.AreEqual(
            "/dotnetcloud.moduleapi.v1.ModuleApi/GetUser",
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "GetUser"));
    }

    [TestMethod]
    public void IsModuleFacingRpc_ModuleServices_ReturnsTrue()
    {
        Assert.IsTrue(CapabilityCatalog.IsModuleFacingRpc(
            CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, "GetUser")));
        Assert.IsTrue(CapabilityCatalog.IsModuleFacingRpc(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "ReportHealth")));
    }

    [TestMethod]
    public void IsModuleFacingRpc_OtherService_ReturnsFalse()
    {
        Assert.IsFalse(CapabilityCatalog.IsModuleFacingRpc("/dotnetcloud.files.v1.FilesUploadStream/Upload"));
    }

    [TestMethod]
    public void ResolveCapability_SameRpcNameOnBothServices_ResolvesIndependently()
    {
        var legacy = CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, "GetCurrentUser"));
        var canonical = CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "GetCurrentUserContext"));

        Assert.AreEqual(nameof(ICurrentUserContext), legacy);
        Assert.AreEqual(nameof(ICurrentUserContext), canonical);
    }

    [TestMethod]
    public void ResolveCapability_LegacyGetUserAndModuleApiGetUser_BothMapToUserDirectory()
    {
        Assert.AreEqual(
            nameof(IUserDirectory),
            CapabilityCatalog.ResolveCapability(
                CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, "GetUser")));
        Assert.AreEqual(
            nameof(IUserDirectory),
            CapabilityCatalog.ResolveCapability(
                CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "GetUser")));
    }

    [TestMethod]
    public void ResolveCapability_SelfServiceRpc_ReturnsNull()
    {
        Assert.IsNull(CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "ReportHealth")));
    }

    [TestMethod]
    public void IsKnownRpc_EveryMappedAndSelfServiceRpc_ReturnsTrue()
    {
        foreach (var rpc in CapabilityCatalog.GetKnownRpcs())
        {
            Assert.IsTrue(CapabilityCatalog.IsKnownRpc(rpc), $"{rpc} should be known.");
        }
    }

    [TestMethod]
    public void IsKnownRpc_UnmappedModuleRpc_ReturnsFalse()
    {
        Assert.IsFalse(CapabilityCatalog.IsKnownRpc(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "SomethingNew")));
    }

    [TestMethod]
    public void ResolveCapability_MethodWithoutLeadingSlash_IsAccepted()
    {
        Assert.AreEqual(
            nameof(IEventBus),
            CapabilityCatalog.ResolveCapability($"{CapabilityCatalog.ModuleApiService}/PublishEvent"));
    }
}
