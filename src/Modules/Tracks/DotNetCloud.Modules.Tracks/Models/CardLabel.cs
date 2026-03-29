namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Join entity representing the many-to-many relationship between cards and labels.
/// </summary>
public sealed class CardLabel
{
    /// <summary>The card that has the label.</summary>
    public Guid CardId { get; set; }

    /// <summary>The label applied to the card.</summary>
    public Guid LabelId { get; set; }

    /// <summary>Timestamp when the label was applied (UTC).</summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }

    /// <summary>Navigation property to the label.</summary>
    public Label? Label { get; set; }
}
