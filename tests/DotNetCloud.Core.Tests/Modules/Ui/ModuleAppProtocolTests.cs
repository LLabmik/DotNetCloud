using DotNetCloud.Core.Modules.Ui;

namespace DotNetCloud.Core.Tests.Modules.Ui;

/// <summary>
/// Tests for the shared module UI plane contract: URL shapes and the postMessage protocol.
/// </summary>
[TestClass]
public class ModuleAppProtocolTests
{
    [TestMethod]
    public void BasePath_ReverseDnsId_IsUnderTheAppsRoot()
    {
        Assert.AreEqual("/apps/vendor.receipts", ModuleAppUrls.BasePath("vendor.receipts"));
    }

    [TestMethod]
    public void ShellPath_ReverseDnsId_IsUnderTheModulesRoot()
    {
        Assert.AreEqual("/modules/vendor.receipts", ModuleAppUrls.ShellPath("vendor.receipts"));
    }

    [TestMethod]
    [DataRow("vendor.receipts/path", "")]
    [DataRow("../escape", "")]
    [DataRow("", "")]
    public void BasePath_UnsafeId_IsEmpty(string moduleId, string expected)
    {
        Assert.AreEqual(expected, ModuleAppUrls.BasePath(moduleId));
    }

    [TestMethod]
    [DataRow(null, "/")]
    [DataRow("", "/")]
    [DataRow("/", "/")]
    [DataRow("/settings", "/settings")]
    [DataRow("settings", "/settings")]
    [DataRow("/settings?tab=1", "/settings")]
    [DataRow("//evil.example.org", "/")]
    [DataRow("https://evil.example.org/x", "/")]
    [DataRow("/../escape", "/")]
    [DataRow(@"\windows\path", "/")]
    public void NormalizeEntryPath_RejectsEscapes(string? entryPath, string expected)
    {
        Assert.AreEqual(expected, ModuleAppUrls.NormalizeEntryPath(entryPath));
    }

    [TestMethod]
    public void EntryUrl_DefaultEntry_EndsWithSlash()
    {
        Assert.AreEqual("/apps/vendor.receipts/", ModuleAppUrls.EntryUrl("vendor.receipts"));
    }

    [TestMethod]
    public void EntryUrl_NestedEntry_IsAppended()
    {
        Assert.AreEqual("/apps/vendor.receipts/settings", ModuleAppUrls.EntryUrl("vendor.receipts", "/settings"));
    }

    [TestMethod]
    public void FromFrame_ContainsEveryFrameOriginatedType()
    {
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Ready));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Navigate));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Resize));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Title));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.OpenExternal));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.RequestConsent));
        Assert.IsTrue(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Notify));
    }

    [TestMethod]
    public void IsFromFrameMessage_HostOnlyOrUnknownType_IsRejected()
    {
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Init));
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.Theme));
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage(ModuleAppProtocol.ConsentResult));
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage("dnc:deleteEverything"));
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage(null));
        Assert.IsFalse(ModuleAppProtocol.IsFromFrameMessage(string.Empty));
    }

    [TestMethod]
    public void IsToFrameMessage_FrameOnlyType_IsRejected()
    {
        Assert.IsFalse(ModuleAppProtocol.IsToFrameMessage(ModuleAppProtocol.Notify));
        Assert.IsFalse(ModuleAppProtocol.IsToFrameMessage(ModuleAppProtocol.Navigate));
        Assert.IsTrue(ModuleAppProtocol.IsToFrameMessage(ModuleAppProtocol.Init));
    }

    [TestMethod]
    public void MessageTypes_AreUniqueAcrossDirections()
    {
        var all = ModuleAppProtocol.FromFrame.Concat(ModuleAppProtocol.ToFrame).ToList();

        Assert.AreEqual(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(all.All(type => type.StartsWith("dnc:", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(0, 200)]
    [DataRow(120, 200)]
    [DataRow(500, 500)]
    [DataRow(999999, 20000)]
    public void ClampHeight_ReturnsValueInsideTheAcceptedRange(double input, int expected)
    {
        Assert.AreEqual(expected, ModuleAppProtocol.ClampHeight(input));
    }
}
