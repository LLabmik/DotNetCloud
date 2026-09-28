namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Deterministic identifiers for calendar reminder alarms and the notifications they raise.
/// </summary>
/// <remarks>
/// <para>
/// Every value produced here must be stable <em>across app processes</em>.
/// <see cref="string.GetHashCode()"/> and <c>HashCode.Combine</c> are seeded with a
/// random value once per process in .NET, so the previous scheme produced a different
/// <c>PendingIntent</c> request code after every restart. Android's <c>AlarmManager</c> keys pending
/// alarms by <c>PendingIntent</c> identity (request code + intent filter), so an alarm armed by an
/// earlier process could neither be replaced nor cancelled by a later one. Alarms therefore
/// accumulated across restarts, boots and SignalR reconnects and all fired at their shared trigger
/// time — a burst of duplicate notifications for a single reminder. The same randomisation made the
/// notification id change per process, so the duplicates stacked instead of replacing each other.
/// </para>
/// </remarks>
public static class CalendarAlarmIdentity
{
    /// <summary>
    /// Stable 31-bit FNV-1a hash. Unlike <see cref="string.GetHashCode()"/> this returns the same
    /// value in every process, so it is safe to use for <c>PendingIntent</c> request codes.
    /// </summary>
    /// <param name="value">The text to hash.</param>
    /// <returns>A non-negative, process-independent hash.</returns>
    public static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// <summary>
    /// Stable identity of a single reminder occurrence (event + occurrence start + offset).
    /// </summary>
    /// <param name="eventId">Identifier of the calendar event.</param>
    /// <param name="occurrenceStartUtc">UTC start of the occurrence the reminder belongs to.</param>
    /// <param name="minutesBefore">Minutes before the occurrence start that the reminder fires.</param>
    /// <returns>An opaque key, identical in every process.</returns>
    public static string Key(Guid eventId, DateTime occurrenceStartUtc, int minutesBefore)
        => $"{eventId:N}|{occurrenceStartUtc.Ticks}|{minutesBefore}";

    /// <summary>
    /// Deterministic <c>PendingIntent</c> request code for a reminder occurrence. Re-using the same
    /// code is what allows a re-schedule to <em>replace</em> (rather than add to) the pending alarm.
    /// </summary>
    /// <param name="eventId">Identifier of the calendar event.</param>
    /// <param name="occurrenceStartUtc">UTC start of the occurrence the reminder belongs to.</param>
    /// <param name="minutesBefore">Minutes before the occurrence start that the reminder fires.</param>
    /// <returns>A stable, non-negative request code.</returns>
    public static int RequestCode(Guid eventId, DateTime occurrenceStartUtc, int minutesBefore)
        => StableHash(Key(eventId, occurrenceStartUtc, minutesBefore));

    /// <summary>
    /// Deterministic notification id for a reminder occurrence, so redelivering the same reminder
    /// updates the existing notification instead of stacking a second one.
    /// </summary>
    /// <param name="eventId">Identifier of the calendar event.</param>
    /// <param name="occurrenceStartUtc">UTC start of the occurrence the reminder belongs to.</param>
    /// <param name="minutesBefore">Minutes before the occurrence start that the reminder fires.</param>
    /// <returns>A notification id in the calendar-reminder range.</returns>
    public static int NotificationId(Guid eventId, DateTime occurrenceStartUtc, int minutesBefore)
        => NotificationIdBase + (StableHash(Key(eventId, occurrenceStartUtc, minutesBefore)) & 0x0FFF);

    /// <summary>Base of the notification-id range reserved for calendar reminders.</summary>
    public const int NotificationIdBase = 3000;
}
