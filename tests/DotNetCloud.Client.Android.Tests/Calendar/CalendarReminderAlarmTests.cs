using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Calendar;

/// <summary>
/// Tests for the calendar reminder alarm rules: identity must be stable across app processes (a
/// per-process identifier meant old alarms could never be replaced or cancelled, so they accumulated
/// and all fired at once), a reminder that became due while the app was not running must be delivered
/// exactly once instead of on every resync, and the notification must report the real time remaining
/// rather than the configured offset.
/// </summary>
[TestClass]
public sealed class CalendarReminderAlarmTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime OccurrenceStart = new(2026, 10, 4, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>A one-week-before reminder — the "in 168 hours" alert from the bug report.</summary>
    private const int OneWeekBefore = 7 * 24 * 60;

    private static readonly IReadOnlySet<string> NothingDelivered =
        new HashSet<string>(StringComparer.Ordinal);

    private static CalendarReminderRecord Reminder(int minutesBefore = 15)
        => new(EventId, OccurrenceStart, minutesBefore);

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Pins the hash to FNV-1a. A randomized hash (<see cref="string.GetHashCode()"/>) could never
    /// reproduce these fixed values, which is exactly why it must not be used for alarm identity.
    /// </summary>
    [TestMethod]
    [DataRow("", 18652613)]
    [DataRow("a", 1678518572)]
    [DataRow("abc", 440920331)]
    [DataRow("dotnetcloud", 1071253168)]
    public void StableHash_MatchesFnv1a_KnownValues(string input, int expected)
        => Assert.AreEqual(expected, CalendarAlarmIdentity.StableHash(input));

    [TestMethod]
    public void StableHash_IsNeverNegative()
        => Assert.IsGreaterThanOrEqualTo(0, CalendarAlarmIdentity.StableHash("some reminder key"));

    [TestMethod]
    public void RequestCode_IsIdentical_ForTheSameReminder()
    {
        // Called "in another process" the code must not change, otherwise AlarmManager treats the
        // re-scheduled reminder as a brand new alarm and the old one stays armed.
        Assert.AreEqual(
            CalendarAlarmIdentity.RequestCode(EventId, OccurrenceStart, OneWeekBefore),
            CalendarAlarmIdentity.RequestCode(EventId, OccurrenceStart, OneWeekBefore));
    }

    [TestMethod]
    public void RequestCode_DiffersByOffsetAndOccurrence()
    {
        var baseline = CalendarAlarmIdentity.RequestCode(EventId, OccurrenceStart, 15);

        Assert.AreNotEqual(baseline, CalendarAlarmIdentity.RequestCode(EventId, OccurrenceStart, 30));
        Assert.AreNotEqual(baseline, CalendarAlarmIdentity.RequestCode(EventId, OccurrenceStart.AddDays(1), 15));
        Assert.AreNotEqual(baseline, CalendarAlarmIdentity.RequestCode(Guid.NewGuid(), OccurrenceStart, 15));
    }

    [TestMethod]
    public void NotificationId_IsStable_AndInsideTheReminderRange()
    {
        var first = CalendarAlarmIdentity.NotificationId(EventId, OccurrenceStart, OneWeekBefore);
        var second = CalendarAlarmIdentity.NotificationId(EventId, OccurrenceStart, OneWeekBefore);

        Assert.AreEqual(first, second);
        Assert.IsGreaterThanOrEqualTo(CalendarAlarmIdentity.NotificationIdBase, first);
        Assert.IsLessThanOrEqualTo(CalendarAlarmIdentity.NotificationIdBase + 0x0FFF, first);
    }

    [TestMethod]
    public void TriggerUtc_IsTheOffsetBeforeTheOccurrence()
        => Assert.AreEqual(
            OccurrenceStart.AddMinutes(-OneWeekBefore),
            Reminder(OneWeekBefore).TriggerUtc);

    // ── Planning ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Plan_FutureReminder_FiresAtItsTriggerTime()
    {
        var now = OccurrenceStart.AddDays(-30);

        var planned = CalendarReminderPlanner.Plan([Reminder(OneWeekBefore)], NothingDelivered, now);

        Assert.HasCount(1, planned);
        Assert.IsFalse(planned[0].IsCatchUp);
        Assert.AreEqual(Reminder(OneWeekBefore).TriggerUtc, planned[0].FireAtUtc);
    }

    [TestMethod]
    public void Plan_OverdueReminder_DeliversOneCatchUpNow()
    {
        // Became due while the phone was off: deliver once, immediately.
        var now = OccurrenceStart.AddDays(-7).AddMinutes(5);

        var planned = CalendarReminderPlanner.Plan([Reminder(OneWeekBefore)], NothingDelivered, now);

        Assert.HasCount(1, planned);
        Assert.IsTrue(planned[0].IsCatchUp);
        Assert.AreEqual(now, planned[0].FireAtUtc);
    }

    /// <summary>
    /// The reported symptom: a long-lead reminder kept re-alerting. Once the occurrence has been
    /// delivered it must never be planned again, however often the client re-schedules.
    /// </summary>
    [TestMethod]
    public void Plan_OverdueReminder_AlreadyDelivered_IsNotPlannedAgain()
    {
        var record = Reminder(OneWeekBefore);
        var delivered = new HashSet<string>(StringComparer.Ordinal) { record.Key };
        var now = OccurrenceStart.AddDays(-6);

        var planned = CalendarReminderPlanner.Plan([record], delivered, now);

        Assert.IsEmpty(planned);
    }

    /// <summary>
    /// Re-scheduling a future reminder must keep exactly one alarm: the stable request code makes a
    /// re-schedule replace the pending alarm rather than add a second one.
    /// </summary>
    [TestMethod]
    public void Plan_RepeatedPasses_KeepASingleAlarmPerReminder()
    {
        var now = OccurrenceStart.AddDays(-30);

        var first = CalendarReminderPlanner.Plan([Reminder(OneWeekBefore)], NothingDelivered, now);
        var second = CalendarReminderPlanner.Plan([Reminder(OneWeekBefore)], NothingDelivered, now);

        Assert.HasCount(1, first);
        Assert.HasCount(1, second);
        Assert.AreEqual(first[0].Reminder.Key, second[0].Reminder.Key);
        Assert.AreEqual(first[0].FireAtUtc, second[0].FireAtUtc);
    }

    [TestMethod]
    public void Plan_DuplicateOccurrences_ArePlannedOnce()
    {
        var now = OccurrenceStart.AddDays(-30);

        var planned = CalendarReminderPlanner.Plan(
            [Reminder(OneWeekBefore), Reminder(OneWeekBefore)], NothingDelivered, now);

        Assert.HasCount(1, planned);
    }

    [TestMethod]
    public void ToCancel_ReturnsAlarmsThatAreNoLongerWanted()
    {
        // Previously armed for next week; the event has since been moved, so the old alarm is stale.
        var stale = Reminder(OneWeekBefore);
        var now = OccurrenceStart.AddDays(-30);
        var planned = CalendarReminderPlanner.Plan(
            [new CalendarReminderRecord(EventId, OccurrenceStart.AddDays(3), 15)], NothingDelivered, now);

        var toCancel = CalendarReminderPlanner.ToCancel([stale], planned);

        Assert.HasCount(1, toCancel);
        Assert.AreEqual(stale.Key, toCancel[0].Key);
    }

    [TestMethod]
    public void ToCancel_KeepsAlarmsThatAreStillWanted()
    {
        var now = OccurrenceStart.AddDays(-30);
        var record = Reminder(OneWeekBefore);
        var planned = CalendarReminderPlanner.Plan([record], NothingDelivered, now);

        Assert.IsEmpty(CalendarReminderPlanner.ToCancel([record], planned));
    }

    [TestMethod]
    public void ToCancel_IncludesDeliveredOccurrences()
    {
        // Once delivered the alarm must be removed from the armed set, not left to fire again.
        var record = Reminder(OneWeekBefore);
        var delivered = new HashSet<string>(StringComparer.Ordinal) { record.Key };
        var planned = CalendarReminderPlanner.Plan([record], delivered, OccurrenceStart.AddDays(-6));

        Assert.IsEmpty(planned);
        Assert.HasCount(1, CalendarReminderPlanner.ToCancel([record], planned));
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Records_RoundTrip_PreservesValuesAndUtcKind()
    {
        var original = new List<CalendarReminderRecord> { Reminder(OneWeekBefore), Reminder(15) };

        var restored = CalendarReminderRecords.Deserialize(CalendarReminderRecords.Serialize(original));

        Assert.HasCount(2, restored);
        CollectionAssert.AreEqual(
            original.Select(r => r.Key).ToList(),
            restored.Select(r => r.Key).ToList());
        Assert.AreEqual(DateTimeKind.Utc, restored[0].OccurrenceStartUtc.Kind);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json")]
    [DataRow("null")]
    [DataRow("{}")]
    public void Records_Deserialize_InvalidPayload_ReturnsEmpty(string payload)
        => Assert.IsEmpty(CalendarReminderRecords.Deserialize(payload));

    [TestMethod]
    public void Records_Prune_DropsOccurrencesOlderThanRetention()
    {
        var now = OccurrenceStart.AddDays(10);
        var old = Reminder(15);
        var recent = new CalendarReminderRecord(EventId, now.AddHours(-1), 15);

        var pruned = CalendarReminderRecords.Prune([old, recent], now, TimeSpan.FromDays(2));

        Assert.HasCount(1, pruned);
        Assert.AreEqual(recent.Key, pruned[0].Key);
    }

    // ── Notification text ─────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(0, "Event is starting now")]
    [DataRow(-1, "Event is starting now")]
    [DataRow(20, "Starts in 20 minutes")]
    [DataRow(1, "Starts in 1 minute")]
    [DataRow(120, "Starts in 2 hours")]
    [DataRow(150, "Starts in 2h 30m")]
    [DataRow(24 * 60, "Starts in 1 day")]
    [DataRow(7 * 24 * 60, "Starts in 7 days")]
    public void Text_ReportsTheTimeActuallyRemaining(int minutesUntilStart, string expected)
    {
        var now = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(expected, CalendarReminderText.FormatBody(now.AddMinutes(minutesUntilStart), now));
    }

    /// <summary>
    /// The text is derived from the real remaining time, never from the configured offset: a reminder
    /// configured one week before an event that fires half an hour early must not announce
    /// "168 hours".
    /// </summary>
    [TestMethod]
    public void Text_DoesNotRepeatTheConfiguredOffset()
    {
        var now = new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc);
        var eventStart = now.AddMinutes(30);

        var body = CalendarReminderText.FormatBody(eventStart, now);

        Assert.AreEqual("Starts in 30 minutes", body);
        Assert.DoesNotContain("168", body);
    }
}
