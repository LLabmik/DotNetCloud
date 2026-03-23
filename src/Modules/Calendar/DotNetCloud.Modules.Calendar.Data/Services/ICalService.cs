using System.Globalization;
using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// iCalendar (RFC 5545) import/export implementation.
/// Handles conversion between internal calendar events and VCALENDAR/VEVENT format.
/// </summary>
public sealed class ICalService : IICalendarService
{
    private readonly CalendarDbContext _db;
    private readonly ICalendarEventService _eventService;
    private readonly ILogger<ICalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ICalService"/> class.
    /// </summary>
    public ICalService(CalendarDbContext db, ICalendarEventService eventService, ILogger<ICalService> logger)
    {
        _db = db;
        _eventService = eventService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExportEventAsync(Guid eventId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await _db.CalendarEvents
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted &&
                (e.Calendar!.OwnerId == caller.UserId ||
                 e.Calendar.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarEventNotFound, "Calendar event not found.");

        return SerializeToICal([calendarEvent]);
    }

    /// <inheritdoc />
    public async Task<string> ExportCalendarAsync(Guid calendarId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var calendar = await _db.Calendars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calendarId &&
                (c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.CalendarNotFound, "Calendar not found.");

        var events = await _db.CalendarEvents
            .Include(e => e.Attendees)
            .Include(e => e.Reminders)
            .AsNoTracking()
            .Where(e => e.CalendarId == calendarId && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        return SerializeToICal(events, calendar.Name);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEventDto>> ImportEventsAsync(Guid calendarId, string icalText, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icalText);

        var parsedEvents = ParseICalEvents(icalText);
        var results = new List<CalendarEventDto>();

        foreach (var parsed in parsedEvents)
        {
            var dto = new CreateCalendarEventDto
            {
                CalendarId = calendarId,
                Title = parsed.Title!,
                Description = parsed.Description,
                Location = parsed.Location,
                StartUtc = parsed.StartUtc,
                EndUtc = parsed.EndUtc,
                IsAllDay = parsed.IsAllDay,
                RecurrenceRule = parsed.RecurrenceRule,
                Url = parsed.Url
            };

            var result = await _eventService.CreateEventAsync(dto, caller, cancellationToken);
            results.Add(result);
        }

        _logger.LogInformation("Imported {Count} events into calendar {CalendarId} by user {UserId}",
            results.Count, calendarId, caller.UserId);

        return results;
    }

    private static string SerializeToICal(IReadOnlyList<CalendarEvent> events, string? calendarName = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//DotNetCloud//Calendar//EN");
        if (calendarName is not null)
        {
            sb.AppendLine($"X-WR-CALNAME:{EscapeICalText(calendarName)}");
        }

        foreach (var e in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{e.Id}");
            sb.AppendLine($"DTSTART:{FormatICalDateTime(e.StartUtc, e.IsAllDay)}");
            sb.AppendLine($"DTEND:{FormatICalDateTime(e.EndUtc, e.IsAllDay)}");
            sb.AppendLine($"SUMMARY:{EscapeICalText(e.Title)}");
            sb.AppendLine($"DTSTAMP:{FormatICalDateTime(e.UpdatedAt, false)}");
            sb.AppendLine($"CREATED:{FormatICalDateTime(e.CreatedAt, false)}");
            sb.AppendLine($"LAST-MODIFIED:{FormatICalDateTime(e.UpdatedAt, false)}");

            if (e.Description is not null)
                sb.AppendLine($"DESCRIPTION:{EscapeICalText(e.Description)}");
            if (e.Location is not null)
                sb.AppendLine($"LOCATION:{EscapeICalText(e.Location)}");
            if (e.Url is not null)
                sb.AppendLine($"URL:{e.Url}");
            if (e.RecurrenceRule is not null)
                sb.AppendLine($"RRULE:{e.RecurrenceRule}");

            sb.AppendLine($"STATUS:{e.Status switch
            {
                CalendarEventStatus.Confirmed => "CONFIRMED",
                CalendarEventStatus.Tentative => "TENTATIVE",
                CalendarEventStatus.Cancelled => "CANCELLED",
                _ => "CONFIRMED"
            }}");

            foreach (var attendee in e.Attendees)
            {
                var role = attendee.Role switch
                {
                    AttendeeRole.Required => "REQ-PARTICIPANT",
                    AttendeeRole.Optional => "OPT-PARTICIPANT",
                    AttendeeRole.Informational => "NON-PARTICIPANT",
                    _ => "REQ-PARTICIPANT"
                };
                var partstat = attendee.Status switch
                {
                    AttendeeStatus.Accepted => "ACCEPTED",
                    AttendeeStatus.Declined => "DECLINED",
                    AttendeeStatus.Tentative => "TENTATIVE",
                    AttendeeStatus.NeedsAction => "NEEDS-ACTION",
                    _ => "NEEDS-ACTION"
                };
                var cn = attendee.DisplayName is not null ? $";CN={EscapeICalText(attendee.DisplayName)}" : "";
                sb.AppendLine($"ATTENDEE;ROLE={role};PARTSTAT={partstat}{cn}:mailto:{attendee.Email}");
            }

            foreach (var reminder in e.Reminders)
            {
                sb.AppendLine("BEGIN:VALARM");
                sb.AppendLine($"ACTION:{(reminder.Method == ReminderMethod.Email ? "EMAIL" : "DISPLAY")}");
                sb.AppendLine($"TRIGGER:-PT{reminder.MinutesBefore}M");
                sb.AppendLine($"DESCRIPTION:Reminder");
                sb.AppendLine("END:VALARM");
            }

            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static List<ParsedEvent> ParseICalEvents(string icalText)
    {
        var events = new List<ParsedEvent>();
        var lines = icalText.Split(["\r\n", "\n"], StringSplitOptions.None);

        ParsedEvent? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line == "BEGIN:VEVENT")
            {
                current = new ParsedEvent();
                continue;
            }

            if (line == "END:VEVENT" && current is not null)
            {
                if (current.Title is not null)
                {
                    events.Add(current);
                }
                current = null;
                continue;
            }

            if (current is null) continue;

            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
                current.Title = UnescapeICalText(line[8..]);
            else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                current.Description = UnescapeICalText(line[12..]);
            else if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                current.Location = UnescapeICalText(line[9..]);
            else if (line.StartsWith("URL:", StringComparison.OrdinalIgnoreCase))
                current.Url = line[4..];
            else if (line.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
                current.RecurrenceRule = line[6..];
            else if (line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
            {
                var (dt, isAllDay) = ParseICalDateTime(line);
                current.StartUtc = dt;
                current.IsAllDay = isAllDay;
            }
            else if (line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
            {
                var (dt, _) = ParseICalDateTime(line);
                current.EndUtc = dt;
            }
        }

        return events;
    }

    private static string FormatICalDateTime(DateTime dt, bool isAllDay)
    {
        return isAllDay
            ? dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : dt.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private static (DateTime dt, bool isAllDay) ParseICalDateTime(string line)
    {
        // Extract the value after the property name and any parameters
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0) return (DateTime.UtcNow, false);

        var value = line[(colonIndex + 1)..].Trim();

        // VALUE=DATE is an all-day event
        var isAllDay = line.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase) && !line.Contains("VALUE=DATE-TIME", StringComparison.OrdinalIgnoreCase);

        if (isAllDay && value.Length == 8)
        {
            if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateOnly))
                return (dateOnly, true);
        }

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dtUtc))
            return (dtUtc, false);

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dtLocal))
            return (dtLocal, false);

        return (DateTime.UtcNow, false);
    }

    private static string EscapeICalText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    private static string UnescapeICalText(string text)
    {
        return text
            .Replace("\\n", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");
    }

    private sealed class ParsedEvent
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string? Url { get; set; }
        public string? RecurrenceRule { get; set; }
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;
        public DateTime EndUtc { get; set; } = DateTime.UtcNow.AddHours(1);
        public bool IsAllDay { get; set; }
    }
}
