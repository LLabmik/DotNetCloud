using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// Expands recurring calendar events into individual occurrence instances
/// for a given date range, merging with concrete (non-recurring) events
/// and applying exception overrides.
/// </summary>
public interface IOccurrenceExpansionService
{
    /// <summary>
    /// Lists all event occurrences (both concrete and expanded) within
    /// [<paramref name="from"/>, <paramref name="to"/>] for the given calendar.
    /// </summary>
    Task<IReadOnlyList<CalendarEventDto>> ListExpandedEventsAsync(
        Guid calendarId,
        CallerContext caller,
        DateTime from,
        DateTime to,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches across all of a user's calendars with recurrence expansion.
    /// </summary>
    Task<IReadOnlyList<CalendarEventDto>> SearchExpandedEventsAsync(
        CallerContext caller,
        string? query = null,
        DateTime? from = null,
        DateTime? to = null,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default);
}
