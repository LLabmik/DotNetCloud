namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents an audit log entry for actions performed on a board.
/// </summary>
public sealed class BoardActivity
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this activity belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>The user who performed the action.</summary>
    public Guid UserId { get; set; }

    /// <summary>Action performed (e.g., "card.created", "card.moved", "list.created").</summary>
    public required string Action { get; set; }

    /// <summary>Type of entity affected (e.g., "Card", "BoardList", "Label").</summary>
    public required string EntityType { get; set; }

    /// <summary>ID of the entity affected.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Additional details as a JSON string.</summary>
    public string? Details { get; set; }

    /// <summary>Timestamp when the activity occurred (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }
}
