using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Represents a reminder / alarm for a calendar event.
/// </summary>
public sealed class EventReminder
{
    /// <summary>Unique identifier for this reminder.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The event this reminder belongs to.</summary>
    public Guid EventId { get; set; }

    /// <summary>Navigation property to the parent event.</summary>
    public CalendarEvent? Event { get; set; }

    /// <summary>How the reminder is delivered.</summary>
    public ReminderMethod Method { get; set; } = ReminderMethod.Notification;

    /// <summary>Minutes before the event start to trigger the reminder.</summary>
    public int MinutesBefore { get; set; }

    /// <summary>User ID who created this reminder (for audit).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>User ID who last updated this reminder (for audit).</summary>
    public Guid? UpdatedByUserId { get; set; }
}
