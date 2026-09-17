using DotNetCloud.Modules.Photos.UI;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests for <see cref="PhotosPaging"/> — the pure paging math behind the Photos grid.
/// The page size is reported by the browser (photos-layout.js) so a page fills the
/// screen while keeping the pager visible at the bottom; these tests lock in the
/// page-count, clamping, resize and slicing rules that depend on it.
/// </summary>
[TestClass]
public class PhotosPagingTests
{
    // ── NormalizePageSize ────────────────────────────────────

    [TestMethod]
    public void NormalizePageSize_ValidSize_ReturnsUnchanged()
    {
        Assert.AreEqual(1, PhotosPaging.NormalizePageSize(1));
        Assert.AreEqual(42, PhotosPaging.NormalizePageSize(42));
        Assert.AreEqual(PhotosPaging.MaxPageSize, PhotosPaging.NormalizePageSize(PhotosPaging.MaxPageSize));
    }

    [TestMethod]
    public void NormalizePageSize_NonPositiveSize_ReturnsOne()
    {
        Assert.AreEqual(1, PhotosPaging.NormalizePageSize(0));
        Assert.AreEqual(1, PhotosPaging.NormalizePageSize(-25));
    }

    [TestMethod]
    public void NormalizePageSize_HugeViewport_IsCapped()
    {
        Assert.AreEqual(PhotosPaging.MaxPageSize, PhotosPaging.NormalizePageSize(int.MaxValue));
        Assert.AreEqual(PhotosPaging.MaxPageSize, PhotosPaging.NormalizePageSize(PhotosPaging.MaxPageSize + 1));
    }

    // ── ComputeTotalPages ────────────────────────────────────

    [TestMethod]
    public void ComputeTotalPages_ExactMultiple_ReturnsExactPageCount()
    {
        Assert.AreEqual(5, PhotosPaging.ComputeTotalPages(100, 20));
    }

    [TestMethod]
    public void ComputeTotalPages_PartialLastPage_RoundsUp()
    {
        Assert.AreEqual(6, PhotosPaging.ComputeTotalPages(101, 20));
        Assert.AreEqual(2, PhotosPaging.ComputeTotalPages(21, 20));
    }

    [TestMethod]
    public void ComputeTotalPages_SingleItem_ReturnsOnePage()
    {
        Assert.AreEqual(1, PhotosPaging.ComputeTotalPages(1, 20));
    }

    [TestMethod]
    public void ComputeTotalPages_EmptyLibrary_ReturnsOnePage()
    {
        // The pager reads "Page 1 of 1" rather than "Page 1 of 0".
        Assert.AreEqual(1, PhotosPaging.ComputeTotalPages(0, 20));
        Assert.AreEqual(1, PhotosPaging.ComputeTotalPages(-3, 20));
    }

    [TestMethod]
    public void ComputeTotalPages_FitsOnOnePage_ReturnsOnePage()
    {
        Assert.AreEqual(1, PhotosPaging.ComputeTotalPages(20, 20));
        Assert.AreEqual(1, PhotosPaging.ComputeTotalPages(3, 20));
    }

    [TestMethod]
    public void ComputeTotalPages_InvalidPageSize_TreatedAsOne()
    {
        Assert.AreEqual(50, PhotosPaging.ComputeTotalPages(50, 0));
    }

    // ── ClampPage ────────────────────────────────────────────

    [TestMethod]
    public void ClampPage_WithinRange_ReturnsUnchanged()
    {
        Assert.AreEqual(2, PhotosPaging.ClampPage(2, 100, 20));
    }

    [TestMethod]
    public void ClampPage_PastLastPage_ReturnsLastPage()
    {
        // 100 photos / 20 per page = 5 pages -> highest valid index is 4.
        Assert.AreEqual(4, PhotosPaging.ClampPage(9, 100, 20));
    }

    [TestMethod]
    public void ClampPage_Negative_ReturnsFirstPage()
    {
        Assert.AreEqual(0, PhotosPaging.ClampPage(-4, 100, 20));
    }

    [TestMethod]
    public void ClampPage_EmptyLibrary_ReturnsFirstPage()
    {
        Assert.AreEqual(0, PhotosPaging.ClampPage(3, 0, 20));
    }

    // ── ComputePageForResize ─────────────────────────────────

    [TestMethod]
    public void ComputePageForResize_FirstPage_StaysZero()
    {
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(0, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_Grow_KeepsApproximatePosition()
    {
        Assert.AreEqual(1, PhotosPaging.ComputePageForResize(36, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_Shrink_KeepsApproximatePosition()
    {
        Assert.AreEqual(8, PhotosPaging.ComputePageForResize(48, 6, 100));
    }

    [TestMethod]
    public void ComputePageForResize_ClampsToLastPage()
    {
        Assert.AreEqual(4, PhotosPaging.ComputePageForResize(96, 20, 100));
        Assert.AreEqual(4, PhotosPaging.ComputePageForResize(97, 20, 100));
    }

    [TestMethod]
    public void ComputePageForResize_NewSizeExceedsTotal_ReturnsZero()
    {
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(12, 50, 25));
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(0, 50, 25));
    }

    [TestMethod]
    public void ComputePageForResize_EmptyLibrary_ReturnsZero()
    {
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(0, 20, 0));
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(12, 20, 0));
    }

    [TestMethod]
    public void ComputePageForResize_InvalidSize_ReturnsZero()
    {
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(12, 0, 100));
        Assert.AreEqual(0, PhotosPaging.ComputePageForResize(12, -5, 100));
    }

    [TestMethod]
    public void ComputePageForResize_PageSizeOne_UsesOffsetAsPage()
    {
        Assert.AreEqual(12, PhotosPaging.ComputePageForResize(12, 1, 100));
    }

    [TestMethod]
    public void ComputePageForResize_SameSize_KeepsPage()
    {
        Assert.AreEqual(8, PhotosPaging.ComputePageForResize(96, 12, 100));
    }

    // ── SlicePage ────────────────────────────────────────────

    [TestMethod]
    public void SlicePage_FirstPage_ReturnsLeadingItems()
    {
        var source = Enumerable.Range(1, 25).ToArray();

        var page = PhotosPaging.SlicePage(source, 0, 10);

        CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), page.ToArray());
    }

    [TestMethod]
    public void SlicePage_MiddlePage_ReturnsWindowOfItems()
    {
        var source = Enumerable.Range(1, 25).ToArray();

        var page = PhotosPaging.SlicePage(source, 1, 10);

        CollectionAssert.AreEqual(Enumerable.Range(11, 10).ToArray(), page.ToArray());
    }

    [TestMethod]
    public void SlicePage_LastPage_ReturnsRemainderOnly()
    {
        var source = Enumerable.Range(1, 25).ToArray();

        var page = PhotosPaging.SlicePage(source, 2, 10);

        CollectionAssert.AreEqual(new[] { 21, 22, 23, 24, 25 }, page.ToArray());
    }

    [TestMethod]
    public void SlicePage_ListFitsOnOnePage_ReturnsSourceInstance()
    {
        var source = Enumerable.Range(1, 8).ToArray();

        // No allocation when a section already fits on one page.
        Assert.AreSame(source, PhotosPaging.SlicePage(source, 0, 10));
    }

    [TestMethod]
    public void SlicePage_PagePastEnd_ReturnsEmpty()
    {
        var source = Enumerable.Range(1, 25).ToArray();

        var page = PhotosPaging.SlicePage(source, 7, 10);

        Assert.AreEqual(0, page.Count);
    }

    [TestMethod]
    public void SlicePage_EmptySource_ReturnsEmpty()
    {
        var page = PhotosPaging.SlicePage(Array.Empty<int>(), 0, 10);

        Assert.AreEqual(0, page.Count);
    }

    [TestMethod]
    public void SlicePage_InvalidPageSize_FallsBackToOneItem()
    {
        var source = Enumerable.Range(1, 25).ToArray();

        var page = PhotosPaging.SlicePage(source, 0, 0);

        Assert.AreEqual(1, page.Count);
    }
}
