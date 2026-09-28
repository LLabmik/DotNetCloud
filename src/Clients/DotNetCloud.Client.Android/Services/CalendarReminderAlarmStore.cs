namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Persists the calendar reminder bookkeeping: the alarms currently armed and the reminder
/// occurrences that have already been delivered.
/// </summary>
/// <remarks>
/// The armed-alarm list is what makes cancellation exact (it covers every offset, not a fixed list)
/// and the delivered list is what stops a already-delivered reminder from re-alerting on every
/// calendar load, boot and SignalR reconnect.
/// </remarks>
internal static class CalendarReminderAlarmStore
{
    /// <summary>Preference key holding the alarms currently armed.</summary>
    internal const string ScheduledKey = "CalendarReminderScheduledAlarms";

    /// <summary>Preference key holding the reminder occurrences already delivered.</summary>
    internal const string DeliveredKey = "CalendarReminderDeliveredOccurrences";

    /// <summary>
    /// How long records are kept after their occurrence starts. Long enough to survive the lifetime of
    /// a reminder, short enough that the bookkeeping cannot grow without bound.
    /// </summary>
    private static readonly TimeSpan RecordRetention = TimeSpan.FromDays(2);

    /// <summary>Returns the alarms recorded as armed.</summary>
    /// <returns>The recorded alarms, empty when nothing is recorded.</returns>
    internal static List<CalendarReminderRecord> GetScheduled()
        => CalendarReminderRecords.Deserialize(Preferences.Default.Get(ScheduledKey, string.Empty));

    /// <summary>Replaces the armed-alarm record, pruning occurrences that finished long ago.</summary>
    /// <param name="records">The alarms currently armed.</param>
    internal static void SetScheduled(IEnumerable<CalendarReminderRecord> records)
        => Preferences.Default.Set(
            ScheduledKey,
            CalendarReminderRecords.Serialize(
                CalendarReminderRecords.Prune(records, DateTime.UtcNow, RecordRetention)));

    /// <summary>Returns the reminder occurrences already delivered.</summary>
    /// <returns>The delivered occurrences, empty when nothing is recorded.</returns>
    internal static List<CalendarReminderRecord> GetDelivered()
        => CalendarReminderRecords.Deserialize(Preferences.Default.Get(DeliveredKey, string.Empty));

    /// <summary>Returns the stable keys of the delivered reminder occurrences.</summary>
    /// <returns>A set of <see cref="CalendarReminderRecord.Key"/> values.</returns>
    internal static IReadOnlySet<string> GetDeliveredKeys()
        => GetDelivered().Select(record => record.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Records a reminder occurrence as delivered, so it is never delivered a second time.
    /// </summary>
    /// <param name="record">The reminder occurrence that was just delivered.</param>
    internal static void MarkDelivered(CalendarReminderRecord record)
    {
        var delivered = CalendarReminderRecords.Prune(
            GetDelivered(), DateTime.UtcNow, RecordRetention);

        if (delivered.Any(existing => string.Equals(existing.Key, record.Key, StringComparison.Ordinal)))
            return;

        delivered.Add(record);

        Preferences.Default.Set(DeliveredKey, CalendarReminderRecords.Serialize(delivered));
    }
}
