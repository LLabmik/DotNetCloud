using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a card template for creating pre-configured cards on a board.
/// </summary>
public sealed class CardTemplate
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this template belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Template name.</summary>
    public required string Name { get; set; }

    /// <summary>Default card title pattern.</summary>
    public string? TitlePattern { get; set; }

    /// <summary>Default card description (Markdown).</summary>
    public string? Description { get; set; }

    /// <summary>Default card priority.</summary>
    public CardPriority Priority { get; set; } = CardPriority.None;

    /// <summary>JSON array of label IDs to apply by default.</summary>
    public string? LabelIdsJson { get; set; }

    /// <summary>JSON-serialized checklist definitions.</summary>
    public string? ChecklistsJson { get; set; }

    /// <summary>The user who created this template.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Timestamp when the template was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the template was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }
}
