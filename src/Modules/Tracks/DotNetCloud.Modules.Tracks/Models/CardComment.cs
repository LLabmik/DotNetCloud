namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a comment on a card. Supports Markdown content.
/// </summary>
public sealed class CardComment
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card this comment belongs to.</summary>
    public Guid CardId { get; set; }

    /// <summary>The user who wrote the comment.</summary>
    public Guid UserId { get; set; }

    /// <summary>Markdown content of the comment.</summary>
    public required string Content { get; set; }

    /// <summary>Whether this comment has been edited after creation.</summary>
    public bool IsEdited { get; set; }

    /// <summary>Whether this comment has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when the comment was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Timestamp when the comment was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the comment was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }
}
