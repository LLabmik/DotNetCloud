namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a subtask checklist on a card.
/// Each checklist contains multiple <see cref="ChecklistItem"/> entries.
/// </summary>
public sealed class CardChecklist
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card this checklist belongs to.</summary>
    public Guid CardId { get; set; }

    /// <summary>Checklist title.</summary>
    public required string Title { get; set; }

    /// <summary>Position within the card for ordering.</summary>
    public double Position { get; set; }

    /// <summary>Timestamp when the checklist was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }

    /// <summary>Items in this checklist.</summary>
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}
