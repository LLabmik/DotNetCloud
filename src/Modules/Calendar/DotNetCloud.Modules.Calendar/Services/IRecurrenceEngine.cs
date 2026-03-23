namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// Represents a single occurrence of a recurring calendar event.
/// </summary>
public sealed record OccurrenceInstance
{
    /// <summary>Occurrence start time in UTC.</summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>Occurrence end time in UTC.</summary>
    public required DateTime EndUtc { get; init; }
}

/// <summary>
/// Parses RFC 5545 RRULE strings and expands recurring events into individual
/// occurrence instances within a given time window.
/// </summary>
public interface IRecurrenceEngine
{
    /// <summary>
    /// Expands a recurring event into concrete occurrence instances within
    /// [<paramref name="windowStart"/>, <paramref name="windowEnd"/>].
    /// </summary>
    /// <param name="rrule">RFC 5545 RRULE value (without the "RRULE:" prefix).</param>
    /// <param name="eventStart">The DTSTART of the master event (UTC).</param>
    /// <param name="eventDuration">Duration of a single occurrence.</param>
    /// <param name="windowStart">Inclusive start of the expansion window (UTC).</param>
    /// <param name="windowEnd">Inclusive end of the expansion window (UTC).</param>
    /// <param name="excludedDates">Original start dates of exception instances to skip.</param>
    /// <param name="maxOccurrences">Safety cap on how many occurrences to return.</param>
    /// <returns>Occurrence instances that fall within the window.</returns>
    IReadOnlyList<OccurrenceInstance> Expand(
        string rrule,
        DateTime eventStart,
        TimeSpan eventDuration,
        DateTime windowStart,
        DateTime windowEnd,
        IReadOnlySet<DateTime>? excludedDates = null,
        int maxOccurrences = 1000);
}
