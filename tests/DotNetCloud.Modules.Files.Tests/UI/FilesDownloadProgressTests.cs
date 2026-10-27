using DotNetCloud.Modules.Files.UI;

namespace DotNetCloud.Modules.Files.Tests.UI;

/// <summary>
/// Tests for <see cref="FilesDownloadProgress"/> — the text shown in the modal while the server
/// prepares a multi-item ZIP download.
/// </summary>
[TestClass]
public sealed class FilesDownloadProgressTests
{
    [TestMethod]
    public void BuildStatus_SingleItem_ReturnsGenericPreparingText()
    {
        var result = FilesDownloadProgress.BuildStatus(1);

        Assert.AreEqual("Preparing download…", result);
    }

    [TestMethod]
    public void BuildStatus_MultipleItems_IncludesItemCount()
    {
        var result = FilesDownloadProgress.BuildStatus(5);

        Assert.AreEqual("Preparing 5 items for download…", result);
    }

    [TestMethod]
    public void BuildStatus_ZeroItems_TreatedAsSingleItem()
    {
        var result = FilesDownloadProgress.BuildStatus(0);

        Assert.AreEqual("Preparing download…", result);
    }

    [TestMethod]
    public void GetHoldTimeMs_PreparationFinishedInstantly_HoldsForTheFullMinimum()
    {
        var result = FilesDownloadProgress.GetHoldTimeMs(0);

        Assert.AreEqual(FilesDownloadProgress.MinimumVisibleMs, result);
    }

    [TestMethod]
    public void GetHoldTimeMs_PreparationPartiallyElapsed_HoldsTheRemainder()
    {
        var result = FilesDownloadProgress.GetHoldTimeMs(FilesDownloadProgress.MinimumVisibleMs / 2);

        Assert.AreEqual(FilesDownloadProgress.MinimumVisibleMs - (FilesDownloadProgress.MinimumVisibleMs / 2), result);
    }

    [TestMethod]
    public void GetHoldTimeMs_PreparationTookLongerThanMinimum_DoesNotHold()
    {
        var result = FilesDownloadProgress.GetHoldTimeMs(FilesDownloadProgress.MinimumVisibleMs + 500);

        Assert.AreEqual(0, result);
    }
}
