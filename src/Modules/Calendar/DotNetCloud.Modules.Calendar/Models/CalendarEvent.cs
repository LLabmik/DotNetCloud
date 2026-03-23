using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Represents a calendar event (meeting, appointment, all-day event, etc.).
/// </summary>
public sealed class CalendarEvent
{
    /// <summary>Unique identifier for this event.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The calendar this event belongs to.</summary>
    public Guid CalendarId { get; set; }

    /// <summary>Navigation property to the parent calendar.</summary>
    public Calendar? Calendar { get; set; }

    /// <summary>Identifier of the user who created this event.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Event title / summary.</summary>
    public required string Title { get; set; }

    /// <summary>Detailed event description (Markdown allowed).</summary>
    public string? Description { get; set; }

    /// <summary>Physical or virtual location for the event.</summary>
    public string? Location { get; set; }

    /// <summary>Event start time in UTC.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Event end time in UTC.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Whether this is an all-day event.</summary>
    public bool IsAllDay { get; set; }

    /// <summary>Event status (Tentative, Confirmed, Cancelled).</summary>
    public CalendarEventStatus Status { get; set; } = CalendarEventStatus.Confirmed;

    /// <summary>Recurrence rule in RFC 5545 RRULE format (e.g., "FREQ=WEEKLY;BYDAY=MO,WE,FR").</summary>
    public string? RecurrenceRule { get; set; }

    /// <summary>For a recurrence exception, the ID of the parent recurring event.</summary>
    public Guid? RecurringEventId { get; set; }

    /// <summary>Navigation property to the parent recurring event.</summary>
    public CalendarEvent? RecurringEvent { get; set; }

    /// <summary>For a recurrence exception, the original occurrence date being replaced.</summary>
    public DateTime? OriginalStartUtc { get; set; }

    /// <summary>Hex color override for this specific event.</summary>
    public string? Color { get; set; }

    /// <summary>URL associated with the event (e.g., video conference link).</summary>
    public string? Url { get; set; }

    /// <summary>ETag for CalDAV sync / conflict detection.</summary>
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Whether this event has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the event was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the event was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the event was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Attendees invited to this event.</summary>
    public ICollection<EventAttendee> Attendees { get; set; } = [];

    /// <summary>Reminders configured for this event.</summary>
    public ICollection<EventReminder> Reminders { get; set; } = [];

    /// <summary>Exception instances for this recurring event.</summary>
    public ICollection<CalendarEvent> Exceptions { get; set; } = [];
}
