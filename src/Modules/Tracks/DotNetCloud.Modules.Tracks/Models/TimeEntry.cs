namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a time tracking entry logged against a card.
/// </summary>
public sealed class TimeEntry
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card this time entry is for.</summary>
    public Guid CardId { get; set; }

    /// <summary>The user who logged this time.</summary>
    public Guid UserId { get; set; }

    /// <summary>Start time (UTC). Null for manual duration-only entries.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>End time (UTC). Null if timer is still running or for manual entries.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Duration in minutes. Computed from start/end or manually entered.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Optional description of the work done.</summary>
    public string? Description { get; set; }

    /// <summary>Timestamp when the entry was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the entry was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }
}
