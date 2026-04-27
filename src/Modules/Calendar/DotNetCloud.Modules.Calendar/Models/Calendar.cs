namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Represents a calendar collection owned by a user.
/// </summary>
public sealed class Calendar
{
    /// <summary>Unique identifier for this calendar.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this calendar.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// When set, this calendar belongs to an organization rather than an individual user.
    /// Organization members have implicit access based on their role.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Display name for the calendar (e.g., "Work", "Personal").</summary>
    public required string Name { get; set; }

    /// <summary>Optional description of the calendar.</summary>
    public string? Description { get; set; }

    /// <summary>Hex color code for UI display (e.g., "#3B82F6").</summary>
    public string? Color { get; set; }

    /// <summary>IANA timezone identifier used as the calendar default (e.g., "America/New_York").</summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>Whether this is the owner's default calendar.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether the calendar is visible in UI by default.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>CalDAV sync-token for change tracking.</summary>
    public string SyncToken { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Whether this calendar has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the calendar was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the calendar was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the calendar was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Events in this calendar.</summary>
    public ICollection<CalendarEvent> Events { get; set; } = [];

    /// <summary>Shares granting access to this calendar.</summary>
    public ICollection<CalendarShare> Shares { get; set; } = [];
}
