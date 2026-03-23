using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// iCalendar (RFC 5545) import/export operations.
/// </summary>
public interface IICalendarService
{
    /// <summary>Exports a single event as iCalendar (VCALENDAR/VEVENT) text.</summary>
    Task<string> ExportEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Exports all events in a calendar as iCalendar text.</summary>
    Task<string> ExportCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Imports events from iCalendar text into a calendar.</summary>
    Task<IReadOnlyList<CalendarEventDto>> ImportEventsAsync(Guid calendarId, string icalText, CallerContext caller, CancellationToken cancellationToken = default);
}
