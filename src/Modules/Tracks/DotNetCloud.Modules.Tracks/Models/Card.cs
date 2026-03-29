using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a work item (card) within a board list.
/// Cards are the primary unit of work in Tracks.
/// </summary>
public sealed class Card
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The list this card belongs to.</summary>
    public Guid ListId { get; set; }

    /// <summary>Card title.</summary>
    public required string Title { get; set; }

    /// <summary>Markdown description body.</summary>
    public string? Description { get; set; }

    /// <summary>Position within the list for ordering. Uses gap-based positioning.</summary>
    public double Position { get; set; }

    /// <summary>Optional due date (UTC).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Card priority level.</summary>
    public CardPriority Priority { get; set; } = CardPriority.None;

    /// <summary>Story points estimate for sprint planning.</summary>
    public int? StoryPoints { get; set; }

    /// <summary>Whether the card has been archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether the card has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when the card was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>ID of the user who created this card.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>ETag for optimistic concurrency.</summary>
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Timestamp when the card was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the card was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the list.</summary>
    public BoardList? List { get; set; }

    /// <summary>Users assigned to this card.</summary>
    public ICollection<CardAssignment> Assignments { get; set; } = new List<CardAssignment>();

    /// <summary>Labels applied to this card.</summary>
    public ICollection<CardLabel> CardLabels { get; set; } = new List<CardLabel>();

    /// <summary>Comments on this card.</summary>
    public ICollection<CardComment> Comments { get; set; } = new List<CardComment>();

    /// <summary>File attachments on this card.</summary>
    public ICollection<CardAttachment> Attachments { get; set; } = new List<CardAttachment>();

    /// <summary>Checklists on this card.</summary>
    public ICollection<CardChecklist> Checklists { get; set; } = new List<CardChecklist>();

    /// <summary>Dependencies where this card depends on another.</summary>
    public ICollection<CardDependency> Dependencies { get; set; } = new List<CardDependency>();

    /// <summary>Dependencies where another card depends on this one.</summary>
    public ICollection<CardDependency> Dependents { get; set; } = new List<CardDependency>();

    /// <summary>Time entries logged against this card.</summary>
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    /// <summary>Sprint assignments for this card.</summary>
    public ICollection<SprintCard> SprintCards { get; set; } = new List<SprintCard>();

    /// <summary>Planning poker sessions for this card.</summary>
    public ICollection<PokerSession> PokerSessions { get; set; } = new List<PokerSession>();
}
