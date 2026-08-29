using DotNetCloud.Modules.Video.Host.Services;
using DotNetCloud.Modules.Video.Services;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="HlsStreamWatchdog.IsIdle"/> — the pure helper the idle watchdog
/// uses to decide whether an HLS stream has been abandoned (no segment/playlist request
/// for longer than the idle timeout).
/// </summary>
[TestClass]
public sealed class HlsStreamWatchdogTests
{
    private static TranscodingJob CreateJob(DateTime createdAt) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        VideoId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CacheKey = "test",
        CreatedAt = createdAt,
        IsHls = true
    };

    [TestMethod]
    public void IsIdle_NeverRequested_RecentCreatedAt_NotIdle()
    {
        var now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateJob(now);
        job.LastSegmentRequestedAt = null;

        Assert.IsFalse(HlsStreamWatchdog.IsIdle(job, now, TimeSpan.FromSeconds(300)));
    }

    [TestMethod]
    public void IsIdle_NeverRequested_CreatedAtPastTimeout_Idle()
    {
        var now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateJob(now.AddMinutes(-10));
        job.LastSegmentRequestedAt = null;

        Assert.IsTrue(HlsStreamWatchdog.IsIdle(job, now, TimeSpan.FromSeconds(300)));
    }

    [TestMethod]
    public void IsIdle_RecentlyRequested_NotIdle()
    {
        var now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateJob(now.AddMinutes(-10));
        job.LastSegmentRequestedAt = now.AddSeconds(-30);

        Assert.IsFalse(HlsStreamWatchdog.IsIdle(job, now, TimeSpan.FromSeconds(300)));
    }

    [TestMethod]
    public void IsIdle_RequestedBeyondTimeout_Idle()
    {
        var now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateJob(now.AddMinutes(-10));
        job.LastSegmentRequestedAt = now.AddMinutes(-6);

        Assert.IsTrue(HlsStreamWatchdog.IsIdle(job, now, TimeSpan.FromSeconds(300)));
    }

    [TestMethod]
    public void IsIdle_ExactlyAtTimeoutBoundary_NotIdle()
    {
        var now = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);
        var job = CreateJob(now);
        job.LastSegmentRequestedAt = now.AddSeconds(-300);

        // Idle is defined as strictly greater than the timeout.
        Assert.IsFalse(HlsStreamWatchdog.IsIdle(job, now, TimeSpan.FromSeconds(300)));
    }
}
