using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// Calendar event CRUD, RSVP, and search operations.
/// </summary>
public interface ICalendarEventService
{
    /// <summary>Creates a new calendar event.</summary>
    Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a calendar event by ID.</summary>
    Task<CalendarEventDto?> GetEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists events for a calendar with optional date range filter.</summary>
    Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid calendarId, CallerContext caller, DateTime? from = null, DateTime? to = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing calendar event.</summary>
    Task<CalendarEventDto> UpdateEventAsync(Guid eventId, UpdateCalendarEventDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a calendar event.</summary>
    Task DeleteEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Records an attendee RSVP response.</summary>
    Task<CalendarEventDto> RsvpAsync(Guid eventId, EventRsvpDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches events across all of a user's calendars.</summary>
    Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(CallerContext caller, string? query = null, DateTime? from = null, DateTime? to = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
}
