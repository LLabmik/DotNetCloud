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

    /// <summary>
    /// Icons referenced by the Files module browser, image gallery view, and
    /// full-screen preview (including slideshow and delete controls). Missing
    /// paths fall back to plain text, which overlaps the button label.
    /// </summary>
    [TestMethod]
    [DataRow("view_list")]
    [DataRow("grid_view")]
    [DataRow("photo_library")]
    [DataRow("slideshow")]
    [DataRow("image")]
    [DataRow("delete")]
    [DataRow("play_arrow")]
    [DataRow("pause")]
    [DataRow("chat_bubble")]
    [DataRow("close")]
    [DataRow("refresh")]
    public void GetPath_IconUsedByFilesBrowserOrGallery_ReturnsPathData(string icon)
    {
        Assert.IsTrue(MaterialSvgIcons.HasPath(icon), $"Icon '{icon}' has no SVG path and would render as text.");
        var path = MaterialSvgIcons.GetPath(icon);
        Assert.IsNotNull(path, $"Icon '{icon}' returned a null path.");
        Assert.IsTrue(path!.Length > 10, $"Icon '{icon}' path data is unexpectedly short.");
    }

    /// <summary>
    /// Icons referenced by the admin broadcast page, the admin navigation entry, and
    /// the broadcast modal dialog (severity icon plus its dismiss control).
    /// </summary>
    [TestMethod]
    [DataRow("campaign")]
    [DataRow("info")]
    [DataRow("warning")]
    [DataRow("error")]
    [DataRow("close")]
    [DataRow("delete")]
    [DataRow("send")]
    [DataRow("schedule")]
    public void GetPath_IconUsedByAdminBroadcast_ReturnsPathData(string icon)
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
