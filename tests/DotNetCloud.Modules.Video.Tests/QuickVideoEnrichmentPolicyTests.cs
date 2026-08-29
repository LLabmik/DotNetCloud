using DotNetCloud.Modules.Video.Data.Services;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class QuickVideoEnrichmentPolicyTests
{
    [TestMethod]
    public void ShouldFastTrack_SingleQuietVideo_ReturnsTrue()
    {
        Assert.IsTrue(QuickVideoEnrichmentPolicy.ShouldFastTrack(1, QuickVideoEnrichmentPolicy.QuietPeriod));
    }

    [TestMethod]
    public void ShouldFastTrack_MaxBatchQuiet_ReturnsTrue()
    {
        Assert.IsTrue(QuickVideoEnrichmentPolicy.ShouldFastTrack(
            QuickVideoEnrichmentPolicy.MaxQuickBatchSize, QuickVideoEnrichmentPolicy.QuietPeriod));
    }

    [TestMethod]
    public void ShouldFastTrack_OverThreshold_ReturnsFalse()
    {
        Assert.IsFalse(QuickVideoEnrichmentPolicy.ShouldFastTrack(
            QuickVideoEnrichmentPolicy.MaxQuickBatchSize + 1, QuickVideoEnrichmentPolicy.QuietPeriod));
    }

    [TestMethod]
    public void ShouldFastTrack_ZeroVideos_ReturnsFalse()
    {
        Assert.IsFalse(QuickVideoEnrichmentPolicy.ShouldFastTrack(0, QuickVideoEnrichmentPolicy.QuietPeriod));
    }

    [TestMethod]
    public void ShouldFastTrack_NotQuietYet_ReturnsFalse()
    {
        var notQuiet = QuickVideoEnrichmentPolicy.QuietPeriod - TimeSpan.FromSeconds(1);
        Assert.IsFalse(QuickVideoEnrichmentPolicy.ShouldFastTrack(3, notQuiet));
    }

    [TestMethod]
    public void ExceedsThreshold_OverThreshold_ReturnsTrue()
    {
        Assert.IsTrue(QuickVideoEnrichmentPolicy.ExceedsThreshold(QuickVideoEnrichmentPolicy.MaxQuickBatchSize + 1));
    }

    [TestMethod]
    public void ExceedsThreshold_AtThreshold_ReturnsFalse()
    {
        Assert.IsFalse(QuickVideoEnrichmentPolicy.ExceedsThreshold(QuickVideoEnrichmentPolicy.MaxQuickBatchSize));
    }
}
