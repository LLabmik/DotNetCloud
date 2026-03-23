namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a calendar collection owned by a user.
/// </summary>
public sealed record CalendarDto
{
    /// <summary>
    /// Unique identifier for the calendar.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this calendar.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Display name for the calendar (e.g., "Work", "Personal").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of the calendar.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Hex color code for UI display (e.g., "#3B82F6").
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// IANA timezone identifier used as the calendar default (e.g., "America/New_York").
    /// </summary>
    public string Timezone { get; init; } = "UTC";

    /// <summary>
    /// Whether this is the owner's default calendar.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Whether the calendar is visible in UI by default.
    /// </summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    /// Whether the calendar has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the calendar was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the calendar was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// CalDAV sync-token for change tracking.
    /// </summary>
    public string? SyncToken { get; init; }
}

/// <summary>
/// Represents a calendar event (meeting, appointment, all-day event, etc.).
/// </summary>
public sealed record CalendarEventDto
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The calendar this event belongs to.
    /// </summary>
    public required Guid CalendarId { get; init; }

    /// <summary>
    /// Identifier of the user who created this event.
    /// </summary>
    public required Guid CreatedByUserId { get; init; }

    /// <summary>
    /// Event title / summary.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed event description (Markdown allowed).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Physical or virtual location for the event.
    /// </summary>
    public string? Location { get; init; }

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

    /// <summary>
    /// Event status.
    /// </summary>
    public CalendarEventStatus Status { get; init; } = CalendarEventStatus.Confirmed;

    /// <summary>
    /// Recurrence rule in RFC 5545 RRULE format (e.g., "FREQ=WEEKLY;BYDAY=MO,WE,FR").
    /// Null for non-recurring events.
    /// </summary>
    public string? RecurrenceRule { get; init; }

    /// <summary>
    /// For a recurrence exception, the ID of the parent recurring event.
    /// </summary>
    public Guid? RecurringEventId { get; init; }

    /// <summary>
    /// For a recurrence exception, the original occurrence date being replaced.
    /// </summary>
    public DateTime? OriginalStartUtc { get; init; }

    /// <summary>
    /// Hex color override for this specific event (overrides calendar color).
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// URL associated with the event (e.g., video conference link).
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Whether the event has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the event was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the event was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Attendees invited to this event.
    /// </summary>
    public IReadOnlyList<EventAttendeeDto> Attendees { get; init; } = [];

    /// <summary>
    /// Reminders configured for this event.
    /// </summary>
    public IReadOnlyList<EventReminderDto> Reminders { get; init; } = [];

    /// <summary>
    /// ETag for CalDAV sync / conflict detection.
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Status of a calendar event.
/// </summary>
public enum CalendarEventStatus
{
    /// <summary>Event is tentatively scheduled.</summary>
    Tentative,

    /// <summary>Event is confirmed.</summary>
    Confirmed,

    /// <summary>Event has been cancelled.</summary>
    Cancelled
}

/// <summary>
/// An attendee of a calendar event.
/// </summary>
public sealed record EventAttendeeDto
{
    /// <summary>
    /// Platform user ID if the attendee is a local user. Null for external attendees.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Email address of the attendee.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Display name of the attendee.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Role of the attendee in the event.
    /// </summary>
    public AttendeeRole Role { get; init; } = AttendeeRole.Required;

    /// <summary>
    /// RSVP status of the attendee.
    /// </summary>
    public AttendeeStatus Status { get; init; } = AttendeeStatus.NeedsAction;
}

/// <summary>
/// Role of an event attendee.
/// </summary>
public enum AttendeeRole
{
    /// <summary>Attendance is required.</summary>
    Required,

    /// <summary>Attendance is optional.</summary>
    Optional,

    /// <summary>The attendee is an informational recipient (FYI).</summary>
    Informational
}

/// <summary>
/// RSVP status of an event attendee.
/// </summary>
public enum AttendeeStatus
{
    /// <summary>Attendee has not yet responded.</summary>
    NeedsAction,

    /// <summary>Attendee accepted the invitation.</summary>
    Accepted,

    /// <summary>Attendee declined the invitation.</summary>
    Declined,

    /// <summary>Attendee tentatively accepted.</summary>
    Tentative
}

/// <summary>
/// A reminder / alarm for a calendar event.
/// </summary>
public sealed record EventReminderDto
{
    /// <summary>
    /// How the reminder is delivered.
    /// </summary>
    public ReminderMethod Method { get; init; } = ReminderMethod.Notification;

    /// <summary>
    /// Minutes before the event start to trigger the reminder.
    /// Negative values mean after start (useful for follow-up reminders).
    /// </summary>
    public required int MinutesBefore { get; init; }
}

/// <summary>
/// Delivery method for a calendar reminder.
/// </summary>
public enum ReminderMethod
{
    /// <summary>In-app notification.</summary>
    Notification,

    /// <summary>Email notification.</summary>
    Email
}

/// <summary>
/// Request DTO for creating a new calendar.
/// </summary>
public sealed record CreateCalendarDto
{
    /// <summary>
    /// Display name for the calendar.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Hex color code for UI display.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// IANA timezone identifier.
    /// </summary>
    public string Timezone { get; init; } = "UTC";
}

/// <summary>
/// Request DTO for updating an existing calendar.
/// </summary>
public sealed record UpdateCalendarDto
{
    /// <summary>
    /// Updated display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Updated description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Updated color code.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Updated timezone.
    /// </summary>
    public string? Timezone { get; init; }

    /// <summary>
    /// Whether the calendar should be visible in UI.
    /// </summary>
    public bool? IsVisible { get; init; }
}

/// <summary>
/// Request DTO for creating a new calendar event.
/// </summary>
public sealed record CreateCalendarEventDto
{
    /// <summary>
    /// The calendar to add the event to.
    /// </summary>
    public required Guid CalendarId { get; init; }

    /// <summary>
    /// Event title / summary.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed event description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Physical or virtual location.
    /// </summary>
    public string? Location { get; init; }

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

    /// <summary>
    /// Recurrence rule in RFC 5545 RRULE format.
    /// </summary>
    public string? RecurrenceRule { get; init; }

    /// <summary>
    /// Color override for this event.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// URL associated with the event.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Attendees to invite.
    /// </summary>
    public IReadOnlyList<EventAttendeeDto> Attendees { get; init; } = [];

    /// <summary>
    /// Reminders to configure.
    /// </summary>
    public IReadOnlyList<EventReminderDto> Reminders { get; init; } = [];
}

/// <summary>
/// Request DTO for updating an existing calendar event.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateCalendarEventDto
{
    /// <summary>
    /// Updated event title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Updated description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Updated location.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Updated start time in UTC.
    /// </summary>
    public DateTime? StartUtc { get; init; }

    /// <summary>
    /// Updated end time in UTC.
    /// </summary>
    public DateTime? EndUtc { get; init; }

    /// <summary>
    /// Updated all-day flag.
    /// </summary>
    public bool? IsAllDay { get; init; }

    /// <summary>
    /// Updated event status.
    /// </summary>
    public CalendarEventStatus? Status { get; init; }

    /// <summary>
    /// Updated recurrence rule.
    /// </summary>
    public string? RecurrenceRule { get; init; }

    /// <summary>
    /// Updated color override.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Updated URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Replacement attendee list. Null means no change.
    /// </summary>
    public IReadOnlyList<EventAttendeeDto>? Attendees { get; init; }

    /// <summary>
    /// Replacement reminder list. Null means no change.
    /// </summary>
    public IReadOnlyList<EventReminderDto>? Reminders { get; init; }
}

/// <summary>
/// RSVP response from an attendee.
/// </summary>
public sealed record EventRsvpDto
{
    /// <summary>
    /// The attendee's response.
    /// </summary>
    public required AttendeeStatus Status { get; init; }

    /// <summary>
    /// Optional comment with the response.
    /// </summary>
    public string? Comment { get; init; }
}
