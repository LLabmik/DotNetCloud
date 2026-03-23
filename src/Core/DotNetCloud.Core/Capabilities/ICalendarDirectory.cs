namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides read-only access to calendar data for cross-module queries.
/// Modules use this capability to look up events without direct data access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability tier:</b> Public — automatically granted to all modules.
/// </para>
/// <para>
/// This capability exposes a read-only view of calendar events. Modules that
/// need to create or modify events must use the Calendar module API directly.
/// </para>
/// </remarks>
public interface ICalendarDirectory : ICapabilityInterface
{
    /// <summary>
    /// Gets the title and time of a calendar event by its ID.
    /// </summary>
    /// <param name="calendarEventId">The calendar event ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The event summary if found; otherwise <c>null</c>.</returns>
    Task<CalendarEventSummary?> GetEventSummaryAsync(
        Guid calendarEventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming events for a user within a time window.
    /// </summary>
    /// <param name="userId">The user whose calendars to query.</param>
    /// <param name="from">Start of the time window (UTC).</param>
    /// <param name="to">End of the time window (UTC).</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<CalendarEventSummary>> GetUpcomingEventsAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        int maxResults = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight summary of a calendar event for cross-module display.
/// </summary>
public sealed record CalendarEventSummary
{
    /// <summary>
    /// The calendar event ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The event title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Event start time in UTC.
    /// </summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>
    /// Event end time in UTC.
    /// </summary>
    public required DateTime EndUtc { get; init; }

    /// <summary>
    /// Whether this is an all-day event.
    /// </summary>
    public bool IsAllDay { get; init; }
}
