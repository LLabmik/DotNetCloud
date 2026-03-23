namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Tracks when a specific reminder has been dispatched for an event
/// occurrence. Prevents duplicate reminder delivery for the same occurrence.
/// </summary>
public sealed class ReminderLog
{
    /// <summary>Unique identifier for this log entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The reminder configuration that triggered this dispatch.</summary>
    public Guid ReminderId { get; set; }

    /// <summary>Navigation property to the reminder.</summary>
    public EventReminder? Reminder { get; set; }

    /// <summary>The calendar event this reminder is for.</summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// The occurrence start time this reminder was dispatched for.
    /// For non-recurring events, this matches the event's StartUtc.
    /// For recurring events, this is the specific occurrence date.
    /// </summary>
    public DateTime OccurrenceStartUtc { get; set; }

    /// <summary>When the reminder was dispatched (UTC).</summary>
    public DateTime TriggeredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Whether dispatch succeeded.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Error message if dispatch failed.</summary>
    public string? ErrorMessage { get; set; }
}
