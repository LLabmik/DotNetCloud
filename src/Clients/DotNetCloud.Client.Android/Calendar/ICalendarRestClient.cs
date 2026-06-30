using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Calendar;

/// <summary>REST client for the Calendar module API.</summary>
public interface ICalendarRestClient
{
    // ── Calendars ──────────────────────────────────────────────────

    /// <summary>Lists all calendars owned by or shared with the current user.</summary>
    Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);

    /// <summary>Gets a single calendar by ID.</summary>
    Task<CalendarDto> GetCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default);

    /// <summary>Creates a new calendar.</summary>
    Task<CalendarDto> CreateCalendarAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarDto dto, CancellationToken ct = default);

    /// <summary>Updates a calendar (patch semantics — all fields optional).</summary>
    Task<CalendarDto> UpdateCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, UpdateCalendarDto dto, CancellationToken ct = default);

    /// <summary>Soft-deletes a calendar and all its events.</summary>
    Task DeleteCalendarAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, CancellationToken ct = default);

    // ── Events ─────────────────────────────────────────────────────

    /// <summary>Lists events in a calendar, optionally filtered by date range and paginated.
    /// Recurring events within the window are automatically expanded.</summary>
    Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(
        string serverBaseUrl, string accessToken,
        Guid calendarId, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default);

    /// <summary>Gets a single event by ID.</summary>
    Task<CalendarEventDto> GetEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default);

    /// <summary>Creates a new event. For recurrence exceptions, set
    /// <see cref="CreateCalendarEventDto.RecurringEventId"/> and
    /// <see cref="CreateCalendarEventDto.OriginalStartUtc"/>.</summary>
    Task<CalendarEventDto> CreateEventAsync(
        string serverBaseUrl, string accessToken,
        CreateCalendarEventDto dto, CancellationToken ct = default);

    /// <summary>Updates an event (patch semantics — all fields optional).</summary>
    Task<CalendarEventDto> UpdateEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, UpdateCalendarEventDto dto, CancellationToken ct = default);

    /// <summary>Soft-deletes an event.</summary>
    Task DeleteEventAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, CancellationToken ct = default);

    /// <summary>RSVPs to an event.</summary>
    Task<CalendarEventDto> RsvpAsync(
        string serverBaseUrl, string accessToken,
        Guid eventId, EventRsvpDto dto, CancellationToken ct = default);

    // ── Search ─────────────────────────────────────────────────────

    /// <summary>Searches events across all user calendars within an optional date range.</summary>
    Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(
        string serverBaseUrl, string accessToken,
        string query, DateTime? from = null, DateTime? to = null,
        int skip = 0, int take = 200, CancellationToken ct = default);
}
