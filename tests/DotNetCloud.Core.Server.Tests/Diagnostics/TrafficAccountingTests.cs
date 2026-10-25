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

    [TestMethod]
    public void AddIn_WithUserId_AttributesToUserAndGlobalTotals()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var userId = Guid.NewGuid();

        accounting.AddIn(userId, 700);
        accounting.AddOut(userId, 300);

        var entry = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(userId, entry.UserId);
        Assert.AreEqual(700, entry.BytesIn);
        Assert.AreEqual(300, entry.BytesOut);
        Assert.AreEqual(700, accounting.BytesIn);
        Assert.AreEqual(300, accounting.BytesOut);
    }

    [TestMethod]
    public void AddIn_KeepAliveSizedWrites_DoNotRefreshActivity()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accounting = new TrafficAccounting(clock);
        var userId = Guid.NewGuid();

        accounting.AddIn(userId, 16);
        clock.Advance(TimeSpan.FromMinutes(4));
        accounting.AddIn(userId, 16);

        var entry = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), entry.LastActivityUtc);
        Assert.IsTrue(entry.IsIdle, "Keep-alive frames must not keep a user active.");
    }

    [TestMethod]
    public void AddIn_CrossingActivityQuantum_RefreshesActivity()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accounting = new TrafficAccounting(clock);
        var userId = Guid.NewGuid();

        accounting.AddIn(userId, 16);
        clock.Advance(TimeSpan.FromMinutes(4));
        accounting.AddIn(userId, UserTrafficEntry.ActivityQuantumBytes);

        var entry = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(clock.GetUtcNow(), entry.LastActivityUtc);
        Assert.IsFalse(entry.IsIdle);
    }

    [TestMethod]
    public void PruneIdle_KeepsUsersWithinGrace_AndRemovesUsersPastIt()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accounting = new TrafficAccounting(clock);
        var recentId = Guid.NewGuid();
        var staleId = Guid.NewGuid();

        accounting.AddIn(staleId, 2_000);
        accounting.AddIn(recentId, 2_000);

        // 4 minutes: both are idle (threshold 3), neither is past the 5-minute grace.
        clock.Advance(TimeSpan.FromMinutes(4));
        accounting.AddIn(recentId, UserTrafficEntry.ActivityQuantumBytes);

        Assert.AreEqual(0, accounting.PruneIdle());
        Assert.AreEqual(2, accounting.GetPerUserSnapshot().Count);

        // 9 minutes since the stale user's last activity — past threshold + grace.
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.AreEqual(1, accounting.PruneIdle());
        var remaining = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(recentId, remaining.UserId);
    }

    [TestMethod]
    public void PruneIdle_ThenReturningUser_StartsFromZero()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accounting = new TrafficAccounting(clock);
        var userId = Guid.NewGuid();

        accounting.AddIn(userId, 5_000);
        clock.Advance(TimeSpan.FromMinutes(10));
        accounting.PruneIdle();

        Assert.AreEqual(0, accounting.GetPerUserSnapshot().Count);

        accounting.AddIn(userId, 10);

        var entry = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(10, entry.BytesIn);
        Assert.IsFalse(entry.IsIdle);
    }

    [TestMethod]
    public void UpdateIdleThreshold_ChangesIdleClassification()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accounting = new TrafficAccounting(clock);
        var userId = Guid.NewGuid();

        accounting.AddIn(userId, 2_000);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.IsFalse(accounting.GetPerUserSnapshot().Single().IsIdle);

        accounting.UpdateIdleThreshold(TimeSpan.FromMinutes(1));

        Assert.IsTrue(accounting.GetPerUserSnapshot().Single().IsIdle);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
