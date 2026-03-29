namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Join entity representing the many-to-many relationship between sprints and cards.
/// </summary>
public sealed class SprintCard
{
    /// <summary>The sprint.</summary>
    public Guid SprintId { get; set; }

    /// <summary>The card assigned to the sprint.</summary>
    public Guid CardId { get; set; }

    /// <summary>Timestamp when the card was added to the sprint (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the sprint.</summary>
    public Sprint? Sprint { get; set; }

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }
}
