namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a single item within a <see cref="CardChecklist"/>.
/// </summary>
public sealed class ChecklistItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The checklist this item belongs to.</summary>
    public Guid ChecklistId { get; set; }

    /// <summary>Item title / description.</summary>
    public required string Title { get; set; }

    /// <summary>Whether this item is completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Position within the checklist for ordering.</summary>
    public double Position { get; set; }

    /// <summary>Optional assignee for this item.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>Timestamp when the item was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the item was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the checklist.</summary>
    public CardChecklist? Checklist { get; set; }
}
