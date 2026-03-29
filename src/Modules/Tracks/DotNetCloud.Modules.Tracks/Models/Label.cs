namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a reusable label defined at the board level.
/// Labels can be applied to cards for categorization and filtering.
/// </summary>
public sealed class Label
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this label belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Label title.</summary>
    public required string Title { get; set; }

    /// <summary>Hex color code for UI display (e.g., "#EF4444").</summary>
    public required string Color { get; set; }

    /// <summary>Timestamp when the label was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }

    /// <summary>Cards that have this label applied.</summary>
    public ICollection<CardLabel> CardLabels { get; set; } = new List<CardLabel>();
}
