using DotNetCloud.Modules.Files.UI;

namespace DotNetCloud.Modules.Files.Tests.UI;

/// <summary>
/// Tests for <see cref="FilesDeleteProgress"/> — the progress text shown next to the spinner
/// while the file browser moves files to the trash and the trash bin deletes them permanently.
/// </summary>
[TestClass]
public sealed class FilesDeleteProgressTests
{
    [TestMethod]
    public void BuildStatus_SingleItemWithName_ReturnsName()
    {
        var result = FilesDeleteProgress.BuildStatus(0, 1, "report.pdf");

        Assert.AreEqual("Deleting report.pdf…", result);
    }

    [TestMethod]
    public void BuildStatus_SingleItemWithoutName_ReturnsGenericText()
    {
        var result = FilesDeleteProgress.BuildStatus(0, 1, null);

        Assert.AreEqual("Deleting…", result);
    }

    [TestMethod]
    public void BuildStatus_SingleItemWithBlankName_ReturnsGenericText()
    {
        var result = FilesDeleteProgress.BuildStatus(0, 1, "   ");

        Assert.AreEqual("Deleting…", result);
    }

    [TestMethod]
    public void BuildStatus_MultipleItemsWithName_ReturnsIndexCountAndName()
    {
        var result = FilesDeleteProgress.BuildStatus(1, 5, "Photos");

        Assert.AreEqual("Deleting 2 of 5: Photos…", result);
    }

    [TestMethod]
    public void BuildStatus_MultipleItemsWithoutName_ReturnsIndexAndCount()
    {
        var result = FilesDeleteProgress.BuildStatus(3, 10, null);

        Assert.AreEqual("Deleting 4 of 10…", result);
    }

    [TestMethod]
    public void BuildStatus_LastItem_ReportsFullCount()
    {
        var result = FilesDeleteProgress.BuildStatus(4, 5, "last.txt");

        Assert.AreEqual("Deleting 5 of 5: last.txt…", result);
    }

    [TestMethod]
    public void BuildStatus_ZeroCount_TreatedAsSingleItem()
    {
        var result = FilesDeleteProgress.BuildStatus(0, 0, "stray.txt");

        Assert.AreEqual("Deleting stray.txt…", result);
    }

    [TestMethod]
    public void GetHoldTimeMs_DeleteFinishedInstantly_HoldsForTheFullMinimum()
    {
        var result = FilesDeleteProgress.GetHoldTimeMs(0);

        Assert.AreEqual(FilesDeleteProgress.MinimumVisibleMs, result);
    }

    [TestMethod]
    public void GetHoldTimeMs_DeletePartiallyElapsed_HoldsTheRemainder()
    {
        var result = FilesDeleteProgress.GetHoldTimeMs(200);

        Assert.AreEqual(FilesDeleteProgress.MinimumVisibleMs - 200, result);
    }

    [TestMethod]
    public void GetHoldTimeMs_DeleteExactlyAtMinimum_HoldsNothing()
    {
        var result = FilesDeleteProgress.GetHoldTimeMs(FilesDeleteProgress.MinimumVisibleMs);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetHoldTimeMs_DeleteSlowerThanMinimum_HoldsNothing()
    {
        var result = FilesDeleteProgress.GetHoldTimeMs(FilesDeleteProgress.MinimumVisibleMs + 1);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void MinimumVisibleMs_IsAtLeastOneSecond()
    {
        Assert.IsGreaterThanOrEqualTo(1000, FilesDeleteProgress.MinimumVisibleMs);
    }
}
