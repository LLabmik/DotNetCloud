namespace DotNetCloud.Modules.Calendar.Models;

/// <summary>
/// Permission level for a calendar share.
/// </summary>
public enum CalendarSharePermission
{
    /// <summary>Can view events but not modify.</summary>
    ReadOnly,

    /// <summary>Can view and modify events.</summary>
    ReadWrite
}

/// <summary>
/// Represents a sharing grant for a calendar to another user or team.
/// </summary>
public sealed class CalendarShare
{
    /// <summary>Unique identifier for this share.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The calendar being shared.</summary>
    public Guid CalendarId { get; set; }

    /// <summary>Navigation property to the shared calendar.</summary>
    public Calendar? Calendar { get; set; }

    /// <summary>The user this calendar is shared with (null if shared with a team).</summary>
    public Guid? SharedWithUserId { get; set; }

    /// <summary>The team this calendar is shared with (null if shared with a user).</summary>
    public Guid? SharedWithTeamId { get; set; }

    /// <summary>Permission level granted.</summary>
    public CalendarSharePermission Permission { get; set; } = CalendarSharePermission.ReadOnly;

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
