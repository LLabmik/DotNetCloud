using DotNetCloud.UI.Shared.Components.DataDisplay;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="MaterialSvgIcons"/>.
/// </summary>
[TestClass]
public class MaterialSvgIconsTests
{
    /// <summary>
    /// Icons referenced by the sidebar "open in new tab" affordance and the
    /// Desktop Sync Client page. If one of these is missing its SVG path, the
    /// <see cref="MaterialIcon"/> component falls back to plain text, so each
    /// must resolve to real path data.
    /// </summary>
    [TestMethod]
    [DataRow("open_in_new")]
    [DataRow("download")]
    [DataRow("sync")]
    [DataRow("computer")]
    [DataRow("cloud")]
    [DataRow("folder")]
    [DataRow("favorite")]
    [DataRow("info")]
    public void GetPath_IconUsedBySidebarOrDesktopSync_ReturnsPathData(string icon)
    {
        Assert.IsTrue(MaterialSvgIcons.HasPath(icon), $"Icon '{icon}' has no SVG path and would render as text.");
        var path = MaterialSvgIcons.GetPath(icon);
        Assert.IsNotNull(path, $"Icon '{icon}' returned a null path.");
        Assert.IsTrue(path!.Length > 10, $"Icon '{icon}' path data is unexpectedly short.");
    }

    [TestMethod]
    public void GetPath_UnknownIcon_ReturnsNull()
    {
        var result = MaterialSvgIcons.GetPath("definitely-not-a-real-icon-xyz");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void HasPath_UnknownIcon_ReturnsFalse()
    {
        var result = MaterialSvgIcons.HasPath("definitely-not-a-real-icon-xyz");

        Assert.IsFalse(result);
    }
}
