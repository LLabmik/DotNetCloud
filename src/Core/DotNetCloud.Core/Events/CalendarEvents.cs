namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new calendar event is created.
/// </summary>
public sealed record CalendarEventCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the newly created calendar event.
    /// </summary>
    public required Guid CalendarEventId { get; init; }

    /// <summary>
    /// The ID of the calendar the event belongs to.
    /// </summary>
    public required Guid CalendarId { get; init; }

    /// <summary>
    /// The title of the calendar event.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The ID of the user who created the event.
    /// </summary>
    public required Guid CreatedByUserId { get; init; }

    /// <summary>
    /// Event start time in UTC.
    /// </summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>
    /// Event end time in UTC.
    /// </summary>
    public required DateTime EndUtc { get; init; }

    /// <summary>
    /// Whether this is a recurring event.
    /// </summary>
    public bool IsRecurring { get; init; }
}

/// <summary>
/// Raised when a calendar event is updated.
/// </summary>
public sealed record CalendarEventUpdatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the updated calendar event.
    /// </summary>
    public required Guid CalendarEventId { get; init; }

    /// <summary>
    /// The ID of the calendar the event belongs to.
    /// </summary>
    public required Guid CalendarId { get; init; }

    /// <summary>
    /// The ID of the user who updated the event.
    /// </summary>
    public required Guid UpdatedByUserId { get; init; }
}

/// <summary>
/// Raised when a calendar event is deleted.
/// </summary>
public sealed record CalendarEventDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted calendar event.
    /// </summary>
    public required Guid CalendarEventId { get; init; }

    /// <summary>
    /// The ID of the calendar the event belonged to.
    /// </summary>
    public required Guid CalendarId { get; init; }

    /// <summary>
    /// The ID of the user who deleted the event.
    /// </summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>
    /// Whether this was a permanent (hard) delete vs. soft delete.
    /// </summary>
    public bool IsPermanent { get; init; }
}

/// <summary>
/// Raised when an attendee responds to a calendar event invitation.
/// </summary>
public sealed record CalendarEventRsvpEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the calendar event.
    /// </summary>
    public required Guid CalendarEventId { get; init; }

    /// <summary>
    /// The ID of the attendee who responded.
    /// </summary>
    public required Guid AttendeeUserId { get; init; }

    /// <summary>
    /// The RSVP status (e.g., "Accepted", "Declined", "Tentative").
    /// </summary>
    public required string Status { get; init; }
}

/// <summary>
/// Raised when a calendar event reminder is triggered.
/// </summary>
public sealed record CalendarReminderTriggeredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the calendar event the reminder is for.
    /// </summary>
    public required Guid CalendarEventId { get; init; }

    /// <summary>
    /// The ID of the user to notify.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The title of the calendar event for display.
    /// </summary>
    public required string EventTitle { get; init; }

    /// <summary>
    /// The event start time for display.
    /// </summary>
    public required DateTime EventStartUtc { get; init; }
}
