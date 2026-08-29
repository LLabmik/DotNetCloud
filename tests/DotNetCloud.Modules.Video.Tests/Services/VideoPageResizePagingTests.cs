using DotNetCloud.Modules.Video.UI;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoPage.ComputePageForResize"/> — the pure helper that
/// keeps the current first-visible video roughly on screen when the page size
/// changes (window resize / sidebar collapse) instead of resetting to page 0.
/// </summary>
[TestClass]
public sealed class VideoPageResizePagingTests
{
    [TestMethod]
    public void ComputePageForResize_FirstPage_StaysZero()
    {
        // 12-item pages, 100 total; resizing to 20 keeps page 0.
        Assert.AreEqual(0, VideoPage.ComputePageForResize(0, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_Grow_KeepsApproximatePosition()
    {
        // Page 3 @ 12/page → first item offset 36; at 20/page the closest page is 1.
        Assert.AreEqual(1, VideoPage.ComputePageForResize(36, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_Shrink_KeepsApproximatePosition()
    {
        // Page 4 @ 12/page → first item offset 48; at 6/page the closest page is 8.
        Assert.AreEqual(8, VideoPage.ComputePageForResize(48, 6, 100));
    }

    [TestMethod]
    public void ComputePageForResize_ClampsToLastPage()
    {
        // Offset 96 (last page @ 12/page) with 100 total → new page 4, last valid page is 4.
        Assert.AreEqual(4, VideoPage.ComputePageForResize(96, 20, 100));
        // Offset beyond last valid page clamps down.
        Assert.AreEqual(4, VideoPage.ComputePageForResize(97, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_NewSizeExceedsTotal_ReturnsZero()
    {
        // 25 items, 50/page → single page.
        Assert.AreEqual(0, VideoPage.ComputePageForResize(12, 50, 25));
        Assert.AreEqual(0, VideoPage.ComputePageForResize(0, 50, 25));
    }

    [TestMethod]
    public void ComputePageForResize_EmptyLibrary_ReturnsZero()
    {
        Assert.AreEqual(0, VideoPage.ComputePageForResize(0, 20, 0));
        Assert.AreEqual(0, VideoPage.ComputePageForResize(12, 20, 0));
    }

    [TestMethod]
    public void ComputePageForResize_InvalidSize_ReturnsZero()
    {
        Assert.AreEqual(0, VideoPage.ComputePageForResize(12, 0, 100));
        Assert.AreEqual(0, VideoPage.ComputePageForResize(12, -5, 100));
    }

    [TestMethod]
    public void ComputePageForResize_PageSizeOne_UsesOffsetAsPage()
    {
        // With 1 item per page the page index equals the offset, clamped to last page.
        Assert.AreEqual(12, VideoPage.ComputePageForResize(12, 1, 100));
    }

    [TestMethod]
    public void ComputePageForResize_SameSize_KeepsPage()
    {
        // Page 8 @ 12/page with 100 total → stays 8.
        Assert.AreEqual(8, VideoPage.ComputePageForResize(96, 12, 100));
    }
}
