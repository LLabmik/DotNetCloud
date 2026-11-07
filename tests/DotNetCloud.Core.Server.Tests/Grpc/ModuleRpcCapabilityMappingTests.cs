using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Grpc.ModuleApi.V1;
using Google.Protobuf.Reflection;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Every RPC on a module-facing service must have an explicit enforcement decision: a capability
/// mapping, or an entry on the self-service allow-list with a documented reason.
/// </summary>
/// <remarks>
/// The test reflects the generated descriptors, so adding an RPC to either proto without updating
/// <see cref="CapabilityCatalog"/> fails here instead of shipping an unenforced endpoint.
/// <c>LogAudit</c>, <c>SubmitSearchIndex</c> and <c>GetGroupsForUser</c> were previously unmapped
/// and therefore callable regardless of grants.
/// </remarks>
[TestClass]
public class ModuleRpcCapabilityMappingTests
{
    [TestMethod]
    public void EveryCoreCapabilitiesRpc_HasEnforcementDecision() =>
        AssertEveryRpcIsKnown(DotNetCloud.Core.Grpc.Capabilities.CoreCapabilities.Descriptor);

    [TestMethod]
    public void EveryModuleApiRpc_HasEnforcementDecision() =>
        AssertEveryRpcIsKnown(ModuleApi.Descriptor);

    [TestMethod]
    [DataRow("BroadcastRealtimeEvent", "IRealtimeBroadcaster")]
    [DataRow("LogAudit", "IAuditLogger")]
    [DataRow("SubmitSearchIndex", "ISearchProvider")]
    [DataRow("GetGroupsForUser", "ITeamDirectory")]
    [DataRow("CleanupAdminSharedFolder", "IAdminSharedFolderCleanup")]
    public void CoreCapabilities_PreviouslyUnenforcedOrRetieredRpc_MapsToExpectedCapability(
        string rpc,
        string expected)
    {
        var capability = CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, rpc));

        Assert.AreEqual(expected, capability);
    }

    [TestMethod]
    [DataRow("ReportHealth")]
    [DataRow("CheckUserConsent")]
    [DataRow("GetGrantedScopes")]
    public void ModuleApi_SelfServiceRpc_IsAllowListedWithoutCapability(string rpc)
    {
        var path = CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, rpc);

        Assert.IsTrue(CapabilityCatalog.IsSelfServiceRpc(path), $"{rpc} should be self-service.");
        Assert.IsNull(CapabilityCatalog.ResolveCapability(path));
    }

    private static void AssertEveryRpcIsKnown(ServiceDescriptor service)
    {
        foreach (var method in service.Methods)
        {
            var path = CapabilityCatalog.Rpc(service.FullName, method.Name);

            Assert.IsTrue(
                CapabilityCatalog.IsKnownRpc(path),
                $"RPC '{path}' has no capability mapping and is not allow-listed, so it would be " +
                "denied at runtime (and was previously unenforced). Add it to CapabilityCatalog.");
        }
    }
}
