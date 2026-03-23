using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// Core calendar CRUD operations.
/// </summary>
public interface ICalendarService
{
    /// <summary>Creates a new calendar.</summary>
    Task<CalendarDto> CreateCalendarAsync(CreateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a calendar by ID.</summary>
    Task<CalendarDto?> GetCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists calendars for the calling user.</summary>
    Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing calendar.</summary>
    Task<CalendarDto> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a calendar.</summary>
    Task DeleteCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default);
}
