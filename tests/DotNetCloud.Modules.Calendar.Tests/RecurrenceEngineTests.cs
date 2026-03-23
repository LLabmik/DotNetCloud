using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="RecurrenceEngine"/> — RFC 5545 RRULE parsing and occurrence expansion.
/// </summary>
[TestClass]
public class RecurrenceEngineTests
{
    private RecurrenceEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _engine = new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance);
    }

    // ─── DAILY ─────────────────────────────────────────────

    [TestMethod]
    public void Expand_DailyCount5_Returns5Occurrences()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;COUNT=5", start, duration, windowStart, windowEnd);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
        Assert.AreEqual(new DateTime(2025, 1, 2, 9, 0, 0, DateTimeKind.Utc), result[1].StartUtc);
        Assert.AreEqual(new DateTime(2025, 1, 5, 9, 0, 0, DateTimeKind.Utc), result[4].StartUtc);
    }

    [TestMethod]
    public void Expand_DailyWithInterval2_ReturnsEveryOtherDay()
    {
        var start = new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromMinutes(30);
        var windowStart = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;INTERVAL=2;COUNT=5", start, duration, windowStart, windowEnd);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
        Assert.AreEqual(new DateTime(2025, 3, 3, 10, 0, 0, DateTimeKind.Utc), result[1].StartUtc);
        Assert.AreEqual(new DateTime(2025, 3, 5, 10, 0, 0, DateTimeKind.Utc), result[2].StartUtc);
    }

    [TestMethod]
    public void Expand_DailyWithUntil_StopsAtUntilDate()
    {
        var start = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;UNTIL=20250605T235959Z", start, duration, windowStart, windowEnd);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(new DateTime(2025, 6, 5, 14, 0, 0, DateTimeKind.Utc), result[4].StartUtc);
    }

    // ─── WEEKLY ────────────────────────────────────────────

    [TestMethod]
    public void Expand_WeeklyMondayWednesdayFriday_ReturnsCorrectDays()
    {
        // Event starts on a Monday
        var start = new DateTime(2025, 1, 6, 9, 0, 0, DateTimeKind.Utc); // Monday
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 1, 12, 23, 59, 59, DateTimeKind.Utc); // 1 week

        var result = _engine.Expand("FREQ=WEEKLY;BYDAY=MO,WE,FR", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(DayOfWeek.Monday, result[0].StartUtc.DayOfWeek);
        Assert.AreEqual(DayOfWeek.Wednesday, result[1].StartUtc.DayOfWeek);
        Assert.AreEqual(DayOfWeek.Friday, result[2].StartUtc.DayOfWeek);
    }

    [TestMethod]
    public void Expand_WeeklyWithCount_RespectsCount()
    {
        var start = new DateTime(2025, 1, 6, 9, 0, 0, DateTimeKind.Utc); // Monday
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        // COUNT=4 with BYDAY=MO,WE means first 4 counted occurrences (Mon, Wed, Mon, Wed)
        var result = _engine.Expand("FREQ=WEEKLY;BYDAY=MO,WE;COUNT=4", start, duration, windowStart, windowEnd);

        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void Expand_WeeklyNoByday_UsesEventStartDay()
    {
        var start = new DateTime(2025, 1, 7, 10, 0, 0, DateTimeKind.Utc); // Tuesday
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 1, 28, 23, 59, 59, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=WEEKLY;COUNT=3", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        foreach (var occ in result)
        {
            Assert.AreEqual(DayOfWeek.Tuesday, occ.StartUtc.DayOfWeek);
        }
    }

    // ─── MONTHLY ───────────────────────────────────────────

    [TestMethod]
    public void Expand_MonthlyByMonthDay15_ReturnsFifteenthOfEachMonth()
    {
        var start = new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 4, 30, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=MONTHLY;BYMONTHDAY=15;COUNT=4", start, duration, windowStart, windowEnd);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual(15, result[0].StartUtc.Day);
        Assert.AreEqual(15, result[1].StartUtc.Day);
        Assert.AreEqual(15, result[2].StartUtc.Day);
        Assert.AreEqual(15, result[3].StartUtc.Day);
        Assert.AreEqual(1, result[0].StartUtc.Month);
        Assert.AreEqual(4, result[3].StartUtc.Month);
    }

    [TestMethod]
    public void Expand_MonthlySecondMonday_ReturnsCorrectDates()
    {
        var start = new DateTime(2025, 1, 13, 10, 0, 0, DateTimeKind.Utc); // 2nd Monday of Jan 2025
        var duration = TimeSpan.FromHours(2);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 4, 30, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=MONTHLY;BYDAY=2MO;COUNT=4", start, duration, windowStart, windowEnd);

        Assert.AreEqual(4, result.Count);
        // Verify all are Mondays
        foreach (var occ in result)
        {
            Assert.AreEqual(DayOfWeek.Monday, occ.StartUtc.DayOfWeek);
        }
        // 2nd Monday: Jan=13, Feb=10, Mar=10, Apr=14
        Assert.AreEqual(13, result[0].StartUtc.Day);
        Assert.AreEqual(10, result[1].StartUtc.Day);
        Assert.AreEqual(10, result[2].StartUtc.Day);
        Assert.AreEqual(14, result[3].StartUtc.Day);
    }

    [TestMethod]
    public void Expand_MonthlyLastFriday_ReturnsCorrectDates()
    {
        var start = new DateTime(2025, 1, 31, 16, 0, 0, DateTimeKind.Utc); // Last Friday of Jan 2025
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 4, 30, 23, 59, 59, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=MONTHLY;BYDAY=-1FR;COUNT=3", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        foreach (var occ in result)
        {
            Assert.AreEqual(DayOfWeek.Friday, occ.StartUtc.DayOfWeek);
        }
        Assert.AreEqual(31, result[0].StartUtc.Day); // Jan last Friday
        Assert.AreEqual(28, result[1].StartUtc.Day); // Feb last Friday
        Assert.AreEqual(28, result[2].StartUtc.Day); // Mar last Friday
    }

    [TestMethod]
    public void Expand_MonthlyNoByDay_UsesDtstartDay()
    {
        var start = new DateTime(2025, 1, 20, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=MONTHLY;COUNT=3", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(20, result[0].StartUtc.Day);
        Assert.AreEqual(20, result[1].StartUtc.Day);
        Assert.AreEqual(20, result[2].StartUtc.Day);
    }

    // ─── YEARLY ────────────────────────────────────────────

    [TestMethod]
    public void Expand_YearlyOnNewYearsDay_ReturnsJan1Each()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromDays(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2028, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=YEARLY;BYMONTH=1;BYMONTHDAY=1;COUNT=4", start, duration, windowStart, windowEnd);

        Assert.AreEqual(4, result.Count);
        for (var i = 0; i < 4; i++)
        {
            Assert.AreEqual(1, result[i].StartUtc.Month);
            Assert.AreEqual(1, result[i].StartUtc.Day);
            Assert.AreEqual(2025 + i, result[i].StartUtc.Year);
        }
    }

    [TestMethod]
    public void Expand_YearlyDefault_UsesEventStartMonthDay()
    {
        var start = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(2);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2028, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=YEARLY;COUNT=3", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
        Assert.AreEqual(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc), result[1].StartUtc);
        Assert.AreEqual(new DateTime(2027, 3, 15, 10, 0, 0, DateTimeKind.Utc), result[2].StartUtc);
    }

    // ─── EXCLUDED DATES ────────────────────────────────────

    [TestMethod]
    public void Expand_WithExcludedDates_SkipsExcludedOccurrences()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var excluded = new HashSet<DateTime>
        {
            new(2025, 1, 3, 9, 0, 0, DateTimeKind.Utc)
        };

        var result = _engine.Expand("FREQ=DAILY;COUNT=5", start, duration, windowStart, windowEnd, excluded);

        Assert.AreEqual(4, result.Count);
        Assert.IsFalse(result.Any(o => o.StartUtc.Day == 3));
    }

    // ─── WINDOWING ─────────────────────────────────────────

    [TestMethod]
    public void Expand_WindowAfterStart_ReturnsOnlyWindowedOccurrences()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        // Window starts on Jan 3
        var windowStart = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 1, 5, 23, 59, 59, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;COUNT=10", start, duration, windowStart, windowEnd);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(new DateTime(2025, 1, 3, 9, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
    }

    [TestMethod]
    public void Expand_EmptyWindow_ReturnsEmpty()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;COUNT=5", start, duration, windowStart, windowEnd);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Expand_MaxOccurrencesCap_RespectsLimit()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY", start, duration, windowStart, windowEnd, maxOccurrences: 10);

        Assert.AreEqual(10, result.Count);
    }

    // ─── DURATION PRESERVATION ─────────────────────────────

    [TestMethod]
    public void Expand_PreservesEventDuration()
    {
        var start = new DateTime(2025, 1, 1, 14, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(2.5);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 1, 3, 23, 59, 59, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=DAILY;COUNT=3", start, duration, windowStart, windowEnd);

        foreach (var occ in result)
        {
            Assert.AreEqual(duration, occ.EndUtc - occ.StartUtc);
        }
    }

    // ─── RRULE PARSING ─────────────────────────────────────

    [TestMethod]
    public void ParseRRule_WithPrefix_StripsPrefix()
    {
        var parsed = RecurrenceEngine.ParseRRule("RRULE:FREQ=DAILY;COUNT=3");

        Assert.AreEqual(RecurrenceEngine.RecurrenceFrequency.Daily, parsed.Frequency);
        Assert.AreEqual(3, parsed.Count);
    }

    [TestMethod]
    public void ParseRRule_WeeklyWithByDay_ParsesCorrectly()
    {
        var parsed = RecurrenceEngine.ParseRRule("FREQ=WEEKLY;BYDAY=MO,WE,FR;INTERVAL=2");

        Assert.AreEqual(RecurrenceEngine.RecurrenceFrequency.Weekly, parsed.Frequency);
        Assert.AreEqual(2, parsed.Interval);
        Assert.AreEqual(3, parsed.ByDay.Count);
        Assert.IsTrue(parsed.ByDay.Any(d => d.DayOfWeek == DayOfWeek.Monday));
        Assert.IsTrue(parsed.ByDay.Any(d => d.DayOfWeek == DayOfWeek.Wednesday));
        Assert.IsTrue(parsed.ByDay.Any(d => d.DayOfWeek == DayOfWeek.Friday));
    }

    [TestMethod]
    public void ParseRRule_MonthlyWithOrdinal_ParsesOrdinalByDay()
    {
        var parsed = RecurrenceEngine.ParseRRule("FREQ=MONTHLY;BYDAY=2MO");

        Assert.AreEqual(1, parsed.ByDay.Count);
        Assert.AreEqual(DayOfWeek.Monday, parsed.ByDay[0].DayOfWeek);
        Assert.AreEqual(2, parsed.ByDay[0].OrdinalWeek);
    }

    [TestMethod]
    public void ParseRRule_NegativeOrdinal_ParsesCorrectly()
    {
        var parsed = RecurrenceEngine.ParseRRule("FREQ=MONTHLY;BYDAY=-1FR");

        Assert.AreEqual(1, parsed.ByDay.Count);
        Assert.AreEqual(DayOfWeek.Friday, parsed.ByDay[0].DayOfWeek);
        Assert.AreEqual(-1, parsed.ByDay[0].OrdinalWeek);
    }

    [TestMethod]
    public void ParseRRule_UntilDate_ParsesCorrectly()
    {
        var parsed = RecurrenceEngine.ParseRRule("FREQ=DAILY;UNTIL=20250331T120000Z");

        Assert.IsNotNull(parsed.Until);
        Assert.AreEqual(new DateTime(2025, 3, 31, 12, 0, 0, DateTimeKind.Utc), parsed.Until.Value);
    }

    [TestMethod]
    public void ParseRRule_ByMonthAndByMonthDay_ParsesCorrectly()
    {
        var parsed = RecurrenceEngine.ParseRRule("FREQ=YEARLY;BYMONTH=1,7;BYMONTHDAY=1,15");

        Assert.AreEqual(2, parsed.ByMonth.Count);
        Assert.IsTrue(parsed.ByMonth.Contains(1));
        Assert.IsTrue(parsed.ByMonth.Contains(7));
        Assert.AreEqual(2, parsed.ByMonthDay.Count);
        Assert.IsTrue(parsed.ByMonthDay.Contains(1));
        Assert.IsTrue(parsed.ByMonthDay.Contains(15));
    }

    // ─── EDGE CASES ────────────────────────────────────────

    [TestMethod]
    public void Expand_InvalidRRule_ReturnsEmpty()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var result = _engine.Expand("INVALID_RULE", start, TimeSpan.FromHours(1),
            start, start.AddDays(30));

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Expand_NullRRule_ThrowsArgumentException()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _engine.Expand(null!, start, TimeSpan.FromHours(1), start, start.AddDays(30)));
    }

    [TestMethod]
    public void Expand_ReversedWindow_ReturnsEmpty()
    {
        var start = new DateTime(2025, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var result = _engine.Expand("FREQ=DAILY;COUNT=5", start, TimeSpan.FromHours(1),
            start.AddDays(30), start); // windowStart > windowEnd

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Expand_MonthlyDay31_HandlesShortMonths()
    {
        // Monthly on the 31st — February has no 31st, so it's skipped
        var start = new DateTime(2025, 1, 31, 9, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=MONTHLY;BYMONTHDAY=31", start, duration, windowStart, windowEnd);

        // Jan=31, Feb=none (28 days), Mar=31, Apr=none (30 days), May=31
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(31, result[0].StartUtc.Day);
        Assert.AreEqual(1, result[0].StartUtc.Month);
        Assert.AreEqual(31, result[1].StartUtc.Day);
        Assert.AreEqual(3, result[1].StartUtc.Month);
        Assert.AreEqual(31, result[2].StartUtc.Day);
        Assert.AreEqual(5, result[2].StartUtc.Month);
    }

    [TestMethod]
    public void Expand_WeeklyBiWeekly_SkipsAlternateWeeks()
    {
        var start = new DateTime(2025, 1, 6, 9, 0, 0, DateTimeKind.Utc); // Monday
        var duration = TimeSpan.FromHours(1);
        var windowStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc);

        var result = _engine.Expand("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO;COUNT=4",
            start, duration, windowStart, windowEnd);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual(new DateTime(2025, 1, 6, 9, 0, 0, DateTimeKind.Utc), result[0].StartUtc);
        Assert.AreEqual(new DateTime(2025, 1, 20, 9, 0, 0, DateTimeKind.Utc), result[1].StartUtc);
        Assert.AreEqual(new DateTime(2025, 2, 3, 9, 0, 0, DateTimeKind.Utc), result[2].StartUtc);
        Assert.AreEqual(new DateTime(2025, 2, 17, 9, 0, 0, DateTimeKind.Utc), result[3].StartUtc);
    }
}
