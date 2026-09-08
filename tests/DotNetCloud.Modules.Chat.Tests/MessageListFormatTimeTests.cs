using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MessageList"/> message-time formatting. Message times are stored
/// as UTC instants and converted to the viewer's browser timezone before display, so the
/// wall-clock post time (and its "today / yesterday" labels) must be computed from the
/// user's local date rather than UTC. These tests exercise <c>FormatLocalTime</c>, which
/// receives already-localized values, with fixed local times for determinism.
/// </summary>
[TestClass]
public class MessageListFormatTimeTests
{
    // ── Relative-age branches (timezone-independent) ───────────────

    [TestMethod]
    public void FormatLocalTime_JustNow_ReturnsJustNow()
    {
        var now = new DateTime(2026, 9, 7, 15, 0, 0);
        var sentAt = now.AddSeconds(-30);

        Assert.AreEqual("just now", MessageList.FormatLocalTime(sentAt, now));
    }

    [TestMethod]
    public void FormatLocalTime_FiveMinutesAgo_Returns5mAgo()
    {
        var now = new DateTime(2026, 9, 7, 15, 0, 0);
        var sentAt = now.AddMinutes(-5);

        Assert.AreEqual("5m ago", MessageList.FormatLocalTime(sentAt, now));
    }

    // ── Wall-clock post-time branches (timezone-aware) ─────────────

    [TestMethod]
    public void FormatLocalTime_SameLocalDay_ReturnsClockTimeOnly()
    {
        var now = new DateTime(2026, 9, 7, 15, 0, 0);
        var sentAt = new DateTime(2026, 9, 7, 9, 30, 0);

        // 12-hour clock with AM/PM indicator (was "09:30" in 24-hour format).
        Assert.AreEqual("9:30 AM", MessageList.FormatLocalTime(sentAt, now));
    }

    [TestMethod]
    public void FormatLocalTime_LocalYesterday_ReturnsYesterdayWithTime()
    {
        var now = new DateTime(2026, 9, 7, 9, 0, 0);
        var sentAt = new DateTime(2026, 9, 6, 23, 15, 0);

        // 12-hour clock with AM/PM indicator (was "Yesterday 23:15" in 24-hour format).
        Assert.AreEqual("Yesterday 11:15 PM", MessageList.FormatLocalTime(sentAt, now));
    }

    [TestMethod]
    public void FormatLocalTime_OlderThanYesterday_ReturnsDateAndTime()
    {
        var now = new DateTime(2026, 9, 7, 9, 0, 0);
        var sentAt = new DateTime(2026, 9, 4, 14, 5, 0);
        var expected = sentAt.ToString("MMM d, h:mm tt");

        Assert.AreEqual(expected, MessageList.FormatLocalTime(sentAt, now));
    }

    // ── Day boundary uses the viewer's local date, not UTC ─────────

    [TestMethod]
    public void FormatLocalTime_DayBoundary_IsYesterdayWhenLocalDateDiffersFromUtc()
    {
        // A message sent at 2026-09-07 00:30 UTC is 2026-09-06 19:30 in a UTC-5 timezone.
        // Locally it is "yesterday" even though its UTC date is today — the label must come
        // from the local calendar, otherwise a viewer at UTC-5 would see a misleading date.
        var localSentAt = new DateTime(2026, 9, 6, 19, 30, 0);
        var localNow = new DateTime(2026, 9, 7, 9, 0, 0);

        // 12-hour clock with AM/PM indicator (was "Yesterday 19:30" in 24-hour format).
        Assert.AreEqual("Yesterday 7:30 PM", MessageList.FormatLocalTime(localSentAt, localNow));
    }

    [TestMethod]
    public void FormatLocalTime_DayBoundary_JustAfterMidnightIsStillToday()
    {
        // A message sent just after local midnight (00:30) on the same local day as "now"
        // must render as a same-day clock time, not be pushed back to "Yesterday" merely
        // because it is close to the midnight boundary.
        var localSentAt = new DateTime(2026, 9, 7, 0, 30, 0);
        var localNow = new DateTime(2026, 9, 7, 9, 0, 0);

        // 12-hour clock with AM/PM indicator (was "00:30" in 24-hour format).
        Assert.AreEqual("12:30 AM", MessageList.FormatLocalTime(localSentAt, localNow));
    }
}
