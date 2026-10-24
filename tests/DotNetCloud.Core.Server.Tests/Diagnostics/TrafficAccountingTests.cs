using DotNetCloud.Core.Server.Diagnostics;

namespace DotNetCloud.Core.Server.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="TrafficAccounting"/>.
/// </summary>
[TestClass]
public sealed class TrafficAccountingTests
{
    [TestMethod]
    public void AddIn_And_AddOut_IgnoreNonPositiveValues()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);

        accounting.AddIn(0);
        accounting.AddIn(-5);
        accounting.AddOut(-1);

        Assert.AreEqual(0, accounting.BytesIn);
        Assert.AreEqual(0, accounting.BytesOut);
    }

    [TestMethod]
    public void Add_Concurrently_NeverLosesBytes()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);

        Parallel.For(0, 1000, _ =>
        {
            accounting.AddIn(10);
            accounting.AddOut(20);
        });

        Assert.AreEqual(10_000, accounting.BytesIn);
        Assert.AreEqual(20_000, accounting.BytesOut);
    }

    [TestMethod]
    public void GetRates_WithFewerThanTwoSamples_ReturnsZero()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        accounting.AddIn(500);
        accounting.RecordSample(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var (inRate, outRate) = accounting.GetRates();

        Assert.AreEqual(0, inRate);
        Assert.AreEqual(0, outRate);
    }

    [TestMethod]
    public void GetRates_ComputesPerSecondAverage()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        accounting.RecordSample(start);
        accounting.AddIn(1000);
        accounting.AddOut(2000);
        accounting.RecordSample(start.AddSeconds(2));

        var (inRate, outRate) = accounting.GetRates();

        Assert.AreEqual(500, inRate, 0.001);
        Assert.AreEqual(1000, outRate, 0.001);
    }

    [TestMethod]
    public void GetRates_NeverNegative_WhenCountersDoNotMove()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        accounting.RecordSample(start);
        accounting.RecordSample(start.AddSeconds(1));

        var (inRate, outRate) = accounting.GetRates();

        Assert.AreEqual(0, inRate);
        Assert.AreEqual(0, outRate);
    }

    [TestMethod]
    public void RecordSample_PrunesToFixedWindow()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 25; i++)
        {
            accounting.RecordSample(start.AddSeconds(i));
        }

        accounting.AddIn(10);
        accounting.RecordSample(start.AddSeconds(25));

        var (inRate, _) = accounting.GetRates();

        // The window keeps the last 10 samples (seconds 16..25), so 10 bytes over
        // 9 seconds — not the full 25-sample history.
        Assert.AreEqual(10.0 / 9.0, inRate, 0.001);
    }
}
