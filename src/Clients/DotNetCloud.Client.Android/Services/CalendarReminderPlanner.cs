using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// One calendar reminder occurrence: the event it belongs to, the occurrence start and the
/// minutes-before offset. Used both for the alarms currently armed and for the reminders that have
/// already been delivered.
/// </summary>
/// <param name="EventId">Identifier of the calendar event.</param>
/// <param name="OccurrenceStartUtc">UTC start of the occurrence the reminder belongs to.</param>
/// <param name="MinutesBefore">Minutes before the occurrence start that the reminder fires.</param>
public sealed record CalendarReminderRecord(Guid EventId, DateTime OccurrenceStartUtc, int MinutesBefore)
{
    /// <summary>Stable identity of this reminder occurrence (see <see cref="CalendarAlarmIdentity"/>).</summary>
    [JsonIgnore]
    public string Key => CalendarAlarmIdentity.Key(EventId, OccurrenceStartUtc, MinutesBefore);

    /// <summary>The instant the reminder becomes due.</summary>
    [JsonIgnore]
    public DateTime TriggerUtc => OccurrenceStartUtc.AddMinutes(-MinutesBefore);
}

/// <summary>An alarm that should be armed now.</summary>
/// <param name="Reminder">The reminder occurrence the alarm belongs to.</param>
/// <param name="FireAtUtc">When the alarm should fire.</param>
/// <param name="IsCatchUp">
/// <c>true</c> when the reminder became due while the app was not running and is being delivered
/// late; <c>false</c> for a freshly scheduled future reminder.
/// </param>
public sealed record PlannedCalendarAlarm(CalendarReminderRecord Reminder, DateTime FireAtUtc, bool IsCatchUp);

/// <summary>
/// The span of occurrence start times a scheduling pass actually has knowledge of.
/// </summary>
/// <remarks>
/// A pass may only cancel alarms for occurrences inside its coverage. The event list a pass is given
/// is always a slice of the user's calendar — the visible month, the resync horizon — so treating it
/// as the complete picture disarms reminders that belong to another slice. That is how browsing to an
/// empty month used to cancel every future reminder, and how a resync used to cancel the alarm of an
/// event further out than its own horizon.
/// </remarks>
/// <param name="FromUtc">Inclusive lower bound of the covered occurrence starts.</param>
/// <param name="ToUtc">Inclusive upper bound of the covered occurrence starts.</param>
public readonly record struct CalendarReminderCoverage(DateTime FromUtc, DateTime ToUtc)
{
    /// <summary>A coverage spanning every occurrence, for a pass that genuinely knows about them all.</summary>
    public static CalendarReminderCoverage Unbounded => new(DateTime.MinValue, DateTime.MaxValue);

    /// <summary>
    /// A coverage for a queried window, widened by a day in each direction. The query window and the
    /// occurrence starts are both UTC, but the server decides which events fall inside the window, so an
    /// occurrence can sit just outside it — and it must stay governed by the pass that fetched its event.
    /// </summary>
    /// <param name="fromUtc">Start of the queried window.</param>
    /// <param name="toUtc">End of the queried window.</param>
    /// <returns>The widened coverage.</returns>
    public static CalendarReminderCoverage Around(DateTime fromUtc, DateTime toUtc)
        => new(fromUtc.AddDays(-1), toUtc.AddDays(1));

    /// <summary>Whether an occurrence start falls inside this coverage.</summary>
    /// <param name="occurrenceStartUtc">UTC start of the occurrence.</param>
    /// <returns><c>true</c> when the occurrence is governed by the pass.</returns>
    public bool Contains(DateTime occurrenceStartUtc)
        => occurrenceStartUtc >= FromUtc && occurrenceStartUtc <= ToUtc;
}

/// <summary>
/// Decides which calendar reminder alarms to arm, which to cancel and which occurrences have already
/// been delivered. Pure logic (no Android types) so the duplicate-alert rules stay unit-testable.
/// </summary>
public static class CalendarReminderPlanner
{
    /// <summary>
    /// Builds the alarms to arm. A reminder whose trigger instant is already in the past is delivered
    /// once as a catch-up alarm — unless its occurrence was already delivered, in which case it is
    /// skipped. Skipping delivered occurrences is what stops a long-lead reminder (e.g. one week
    /// before an event) from re-alerting on every calendar load, boot and SignalR reconnect.
    /// </summary>
    /// <param name="desired">Reminders configured on the events the client currently knows about.</param>
    /// <param name="deliveredKeys">Keys of occurrences that have already been delivered.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    /// <returns>The alarms to arm, de-duplicated by occurrence.</returns>
    public static IReadOnlyList<PlannedCalendarAlarm> Plan(
        IEnumerable<CalendarReminderRecord> desired,
        IReadOnlySet<string> deliveredKeys,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(deliveredKeys);

        var planned = new List<PlannedCalendarAlarm>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reminder in desired)
        {
            if (!seen.Add(reminder.Key))
                continue;

            if (reminder.TriggerUtc <= nowUtc)
            {
                // Due while we were not running: deliver once, then never again.
                if (deliveredKeys.Contains(reminder.Key))
                    continue;

                planned.Add(new PlannedCalendarAlarm(reminder, nowUtc, IsCatchUp: true));
            }
            else
            {
                planned.Add(new PlannedCalendarAlarm(reminder, reminder.TriggerUtc, IsCatchUp: false));
            }
        }

        return planned;
    }

    /// <summary>
    /// Alarms that were armed previously but are no longer wanted — the event was deleted or edited,
    /// the reminder removed, its offset changed, or the occurrence has since been delivered. These
    /// must be actively cancelled, otherwise they stay armed and fire as duplicates.
    /// </summary>
    /// <param name="previouslyScheduled">The records persisted by the last scheduling pass.</param>
    /// <param name="planned">The records the current pass is arming.</param>
    /// <param name="coverage">The occurrences this pass has knowledge of.</param>
    /// <returns>The records whose alarms should be cancelled.</returns>
    /// <remarks>
    /// Only records inside <paramref name="coverage"/> are ever cancelled: a pass that only fetched
    /// the visible month must not disarm a reminder that belongs to another month, and the resync pass
    /// must not disarm one outside its horizon.
    /// </remarks>
    public static IReadOnlyList<CalendarReminderRecord> ToCancel(
        IEnumerable<CalendarReminderRecord> previouslyScheduled,
        IEnumerable<PlannedCalendarAlarm> planned,
        CalendarReminderCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(previouslyScheduled);
        ArgumentNullException.ThrowIfNull(planned);

        var stillWanted = new HashSet<string>(planned.Select(p => p.Reminder.Key), StringComparer.Ordinal);

        return previouslyScheduled
            .Where(record => coverage.Contains(record.OccurrenceStartUtc))
            .Where(record => !stillWanted.Contains(record.Key))
            .DistinctBy(record => record.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether an alarm that has just fired may post its notification. Delivery is the point that must
    /// never duplicate, so this is asked again immediately before the notification is built — it
    /// catches both an occurrence delivered by a previous process and one the running scheduler just
    /// re-armed before the delivery was recorded.
    /// </summary>
    /// <param name="deliveredKeys">The occurrences already recorded as delivered.</param>
    /// <param name="record">The occurrence the firing alarm belongs to.</param>
    /// <returns><c>false</c> when the occurrence has already been delivered.</returns>
    public static bool ShouldDeliver(IReadOnlySet<string> deliveredKeys, CalendarReminderRecord record)
    {
        ArgumentNullException.ThrowIfNull(deliveredKeys);
        ArgumentNullException.ThrowIfNull(record);

        return !deliveredKeys.Contains(record.Key);
    }
}

/// <summary>Persistence helpers for the reminder bookkeeping stored in app preferences.</summary>
public static class CalendarReminderRecords
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes the records to the JSON form persisted in preferences.</summary>
    /// <param name="records">The records to serialize.</param>
    /// <returns>A JSON array.</returns>
    public static string Serialize(IEnumerable<CalendarReminderRecord> records)
        => JsonSerializer.Serialize(records.ToList(), JsonOptions);

    /// <summary>
    /// Deserializes records written by <see cref="Serialize"/>. Unreadable or absent payloads yield
    /// an empty list (never throws), so corrupt preferences cannot break reminder scheduling.
    /// </summary>
    /// <param name="json">The persisted payload, if any.</param>
    /// <returns>The records, with timestamps normalized to <see cref="DateTimeKind.Utc"/>.</returns>
    public static List<CalendarReminderRecord> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var records = JsonSerializer.Deserialize<List<CalendarReminderRecord>>(json, JsonOptions) ?? [];
            return records
                .Select(record => record with
                {
                    OccurrenceStartUtc = DateTime.SpecifyKind(record.OccurrenceStartUtc, DateTimeKind.Utc)
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Drops the record with the same identity as <paramref name="record"/>, if present.
    /// </summary>
    /// <param name="records">The records to filter.</param>
    /// <param name="record">The occurrence to remove.</param>
    /// <returns>The remaining records.</returns>
    public static List<CalendarReminderRecord> Except(
        IEnumerable<CalendarReminderRecord> records, CalendarReminderRecord record)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(record);

        return records
            .Where(existing => !string.Equals(existing.Key, record.Key, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Drops records for occurrences that finished longer ago than <paramref name="retention"/>, so the
    /// bookkeeping cannot grow without bound.
    /// </summary>
    /// <param name="records">The records to prune.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    /// <param name="retention">How long an occurrence is kept after it starts.</param>
    /// <returns>The retained records.</returns>
    public static List<CalendarReminderRecord> Prune(
        IEnumerable<CalendarReminderRecord> records,
        DateTime nowUtc,
        TimeSpan retention)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Where(record => record.OccurrenceStartUtc >= nowUtc - retention)
            .ToList();
    }
}
