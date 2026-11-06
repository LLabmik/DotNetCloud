using DotNetCloud.Core.Server.Grpc.Interceptors;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests that every RPC on the module-facing <c>CoreCapabilities</c> service maps to a capability.
/// </summary>
/// <remarks>
/// An unmapped RPC is enforced by nothing. <c>LogAudit</c>, <c>SubmitSearchIndex</c> and
/// <c>GetGroupsForUser</c> previously fell through the interceptor, so any authenticated module
/// could use them regardless of its grants. Adding an RPC to
/// <c>module_capabilities.proto</c> without adding it here must fail this suite.
/// </remarks>
[TestClass]
public class CapabilityGrantMappingTests
{
    // Source of truth: src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto
    private static readonly string[] CoreCapabilitiesRpcs =
    [
        "GetUser",
        "SearchUsers",
        "GetCurrentUser",
        "SendNotification",
        "PublishEvent",
        "GetSetting",
        "SetSetting",
        "CleanupAdminSharedFolder",
        "GetGroup",
        "GetGroupsForUser",
        "GetTeam",
        "GetTeamsForUser",
        "BroadcastRealtimeEvent",
        "LogAudit",
        "SubmitSearchIndex"
    ];

    [TestMethod]
    public void ResolveCapability_EveryCoreCapabilitiesRpc_MapsToACapability()
    {
        foreach (var rpc in CoreCapabilitiesRpcs)
        {
            var capability = CapabilityGrantInterceptor.ResolveCapability($"/dotnetcloud.capabilities.CoreCapabilities/{rpc}");

            Assert.IsNotNull(capability, $"RPC '{rpc}' is not mapped to a capability and is therefore unenforced.");
        }
    }

    [TestMethod]
    [DataRow("BroadcastRealtimeEvent", "IRealtimeBroadcaster")]
    [DataRow("LogAudit", "IAuditLogger")]
    [DataRow("SubmitSearchIndex", "ISearchProvider")]
    [DataRow("GetGroupsForUser", "ITeamDirectory")]
    [DataRow("CleanupAdminSharedFolder", "IAdminSharedFolderCleanup")]
    public void ResolveCapability_PreviouslyUnenforcedRpc_MapsToExpectedCapability(string rpc, string expected)
    {
        var capability = CapabilityGrantInterceptor.ResolveCapability(
            $"/dotnetcloud.capabilities.CoreCapabilities/{rpc}");

        Assert.AreEqual(expected, capability);
    }

    [TestMethod]
    public void ResolveCapability_UnmappedMethod_ReturnsNull()
    {
        var capability = CapabilityGrantInterceptor.ResolveCapability(
            "/dotnetcloud.capabilities.CoreCapabilities/SomeFutureRpc");

        Assert.IsNull(capability);
    }

    [TestMethod]
    public void ResolveCapability_MethodWithoutPackagePrefix_StillResolves()
    {
        var capability = CapabilityGrantInterceptor.ResolveCapability("LogAudit");

        Assert.AreEqual("IAuditLogger", capability);
    }
}
