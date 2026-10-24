using DotNetCloud.Core.Services;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="BackgroundServiceTracker"/> running-state tracking.
/// </summary>
[TestClass]
public sealed class BackgroundServiceTrackerTests
{
    [TestMethod]
    public void RecordRunStarting_CreatesEntry_AndMarksRunning()
    {
        var tracker = new BackgroundServiceTracker();

        tracker.RecordRunStarting("Backup Hosted Service");

        var status = tracker.GetAll()["Backup Hosted Service"];
        Assert.IsTrue(status.IsRunning);
        Assert.IsNotNull(status.CurrentRunStartedAt);
    }

    [TestMethod]
    public void RecordRun_AfterStart_ClearsRunningState()
    {
        var tracker = new BackgroundServiceTracker();

        tracker.RecordRunStarting("Backup Hosted Service");
        tracker.RecordRun("Backup Hosted Service", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), success: true, "ok");

        var status = tracker.GetAll()["Backup Hosted Service"];
        Assert.IsFalse(status.IsRunning);
        Assert.IsNull(status.CurrentRunStartedAt);
        Assert.IsTrue(status.LastRunSuccess);
        Assert.AreEqual(1, status.TotalRuns);
        Assert.AreEqual("ok", status.LastRunMessage);
    }

    [TestMethod]
    public void RecordRun_Failure_IncrementsFailures_AndClearsRunningState()
    {
        var tracker = new BackgroundServiceTracker();

        tracker.RecordRunStarting("Backup Hosted Service");
        tracker.RecordRun("Backup Hosted Service", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), success: false, "boom");

        var status = tracker.GetAll()["Backup Hosted Service"];
        Assert.IsFalse(status.IsRunning);
        Assert.IsFalse(status.LastRunSuccess);
        Assert.AreEqual(1, status.TotalFailures);
        Assert.AreEqual(0, status.TotalRuns);
        Assert.AreEqual("boom", status.LastRunMessage);
    }

    [TestMethod]
    public void ConcurrentStarts_DoNotThrow()
    {
        var tracker = new BackgroundServiceTracker();

        Parallel.For(0, 200, _ => tracker.RecordRunStarting("Backup Hosted Service"));

        Assert.IsTrue(tracker.GetAll()["Backup Hosted Service"].IsRunning);
    }

    [TestMethod]
    public async Task RunTrackedAsync_RecordsStartAndCompletion()
    {
        var tracker = new BackgroundServiceTracker();
        var startedWhileRunning = false;

        await tracker.RunTrackedAsync("Search Reindex", () =>
        {
            startedWhileRunning = tracker.GetAll()["Search Reindex"].IsRunning;
            return Task.CompletedTask;
        });

        var status = tracker.GetAll()["Search Reindex"];
        Assert.IsTrue(startedWhileRunning, "IsRunning must be true while the cycle executes.");
        Assert.IsFalse(status.IsRunning);
        Assert.IsTrue(status.LastRunSuccess);
    }

    [TestMethod]
    public async Task RunTrackedAsync_OnException_RecordsFailure_AndRethrows()
    {
        var tracker = new BackgroundServiceTracker();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            tracker.RunTrackedAsync("Search Reindex", () => throw new InvalidOperationException("boom")));

        var status = tracker.GetAll()["Search Reindex"];
        Assert.IsFalse(status.IsRunning);
        Assert.IsFalse(status.LastRunSuccess);
        Assert.AreEqual("boom", status.LastRunMessage);
    }

    [TestMethod]
    public async Task RunTrackedAsync_WithNullTracker_IsNoOp()
    {
        IBackgroundServiceTracker? tracker = null;

        await tracker.RunTrackedAsync("Anything", () => Task.CompletedTask);
    }
}
