using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Represents an attendee of a calendar event.
/// </summary>
public sealed class EventAttendee
{
    /// <summary>Unique identifier for this attendee record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The event this attendee belongs to.</summary>
    public Guid EventId { get; set; }

    /// <summary>Navigation property to the parent event.</summary>
    public CalendarEvent? Event { get; set; }

    /// <summary>Platform user ID if the attendee is a local user. Null for external attendees.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Email address of the attendee.</summary>
    public required string Email { get; set; }

    /// <summary>Display name of the attendee.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Role of the attendee in the event.</summary>
    public AttendeeRole Role { get; set; } = AttendeeRole.Required;

    /// <summary>RSVP status of the attendee.</summary>
    public AttendeeStatus Status { get; set; } = AttendeeStatus.NeedsAction;

    /// <summary>Optional RSVP comment.</summary>
    public string? Comment { get; set; }

    /// <summary>When the attendee last responded (UTC).</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>User ID who created this attendee record (for audit).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>User ID who last updated this attendee record (for audit).</summary>
    public Guid? UpdatedByUserId { get; set; }
}
