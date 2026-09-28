namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Builds the text of a calendar reminder notification from the <em>actual</em> time remaining until
/// the event, rather than from the configured minutes-before offset.
/// </summary>
/// <remarks>
/// The previous wording ("Starts in {offset}") was wrong whenever the notification was not delivered
/// exactly on time — most visibly for a catch-up delivery, where a reminder configured one week
/// before an event announced "Starts in 168 hours" while the event was in fact much closer, or for a
/// reminder that became due while the device was off.
/// </remarks>
public static class CalendarReminderText
{
    /// <summary>Formats the notification body describing when the event starts.</summary>
    /// <param name="eventStartUtc">UTC start of the event/occurrence.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    /// <returns>Human-readable time remaining, e.g. <c>"Starts in 15 minutes"</c>.</returns>
    public static string FormatBody(DateTime eventStartUtc, DateTime nowUtc)
    {
        var remaining = eventStartUtc - nowUtc;

        if (remaining <= TimeSpan.Zero)
            return "Event is starting now";

        if (remaining.TotalMinutes < 1)
            return "Starts in less than a minute";

        if (remaining.TotalMinutes < 60)
        {
            var minutes = (int)Math.Floor(remaining.TotalMinutes);
            return $"Starts in {minutes} minute{(minutes == 1 ? string.Empty : "s")}";
        }

        if (remaining.TotalHours < 24)
        {
            var hours = (int)Math.Floor(remaining.TotalHours);
            var minutes = (int)Math.Floor(remaining.TotalMinutes) - (hours * 60);
            return minutes > 0
                ? $"Starts in {hours}h {minutes}m"
                : $"Starts in {hours} hour{(hours == 1 ? string.Empty : "s")}";
        }

        var days = (int)Math.Floor(remaining.TotalDays);
        return $"Starts in {days} day{(days == 1 ? string.Empty : "s")}";
    }
}
