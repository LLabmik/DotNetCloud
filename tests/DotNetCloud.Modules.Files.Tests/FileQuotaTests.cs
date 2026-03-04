using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="FileQuota"/> entity covering defaults and computed properties.
/// </summary>
[TestClass]
public class FileQuotaTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsGenerated()
    {
        var quota = new FileQuota();

        Assert.AreNotEqual(Guid.Empty, quota.Id);
    }

    [TestMethod]
    public void WhenCreatedThenMaxBytesIsZero()
    {
        var quota = new FileQuota();

        Assert.AreEqual(0, quota.MaxBytes);
    }

    [TestMethod]
    public void WhenCreatedThenUsedBytesIsZero()
    {
        var quota = new FileQuota();

        Assert.AreEqual(0, quota.UsedBytes);
    }

    [TestMethod]
    public void WhenMaxBytesIsZeroThenUsagePercentIsZero()
    {
        var quota = new FileQuota { MaxBytes = 0, UsedBytes = 1000 };

        Assert.AreEqual(0.0, quota.UsagePercent);
    }

    [TestMethod]
    public void WhenMaxBytesIsZeroThenRemainingBytesIsMaxLong()
    {
        var quota = new FileQuota { MaxBytes = 0 };

        Assert.AreEqual(long.MaxValue, quota.RemainingBytes);
    }

    [TestMethod]
    public void WhenHalfUsedThenUsagePercentIsFifty()
    {
        var quota = new FileQuota { MaxBytes = 1000, UsedBytes = 500 };

        Assert.AreEqual(50.0, quota.UsagePercent, 0.01);
    }

    [TestMethod]
    public void WhenFullyUsedThenUsagePercentIsOneHundred()
    {
        var quota = new FileQuota { MaxBytes = 1000, UsedBytes = 1000 };

        Assert.AreEqual(100.0, quota.UsagePercent, 0.01);
    }

    [TestMethod]
    public void WhenOverLimitThenUsagePercentExceedsOneHundred()
    {
        var quota = new FileQuota { MaxBytes = 1000, UsedBytes = 1500 };

        Assert.IsTrue(quota.UsagePercent > 100.0);
    }

    [TestMethod]
    public void WhenPartiallyUsedThenRemainingBytesIsCorrect()
    {
        var quota = new FileQuota { MaxBytes = 1000, UsedBytes = 300 };

        Assert.AreEqual(700, quota.RemainingBytes);
    }

    [TestMethod]
    public void WhenOverLimitThenRemainingBytesIsZero()
    {
        var quota = new FileQuota { MaxBytes = 1000, UsedBytes = 1500 };

        Assert.AreEqual(0, quota.RemainingBytes);
    }

    [TestMethod]
    public void WhenCreatedThenTimestampsAreRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var quota = new FileQuota();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(quota.CreatedAt >= before && quota.CreatedAt <= after);
        Assert.IsTrue(quota.UpdatedAt >= before && quota.UpdatedAt <= after);
        Assert.IsTrue(quota.LastCalculatedAt >= before && quota.LastCalculatedAt <= after);
    }
}
