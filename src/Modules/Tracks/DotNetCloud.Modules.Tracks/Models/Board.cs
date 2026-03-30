namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a project board — the top-level container for lists, cards, and team collaboration.
/// </summary>
public sealed class Board
{
    /// <summary>Unique identifier for this board.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Board title.</summary>
    public required string Title { get; set; }

    /// <summary>Optional Markdown description.</summary>
    public string? Description { get; set; }

    /// <summary>ID of the user who created the board.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Optional Core team ID that owns this board. Null for personal boards.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Hex color code for UI display (e.g., "#3B82F6").</summary>
    public string? Color { get; set; }

    /// <summary>Whether the board has been archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether the board has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when the board was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>ETag for optimistic concurrency.</summary>
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Timestamp when the board was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the board was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Lists (columns) belonging to this board.</summary>
    public ICollection<BoardSwimlane> Swimlanes { get; set; } = new List<BoardSwimlane>();

    /// <summary>Members who have access to this board.</summary>
    public ICollection<BoardMember> Members { get; set; } = new List<BoardMember>();

    /// <summary>Labels defined for this board.</summary>
    public ICollection<Label> Labels { get; set; } = new List<Label>();

    /// <summary>Sprints defined for this board.</summary>
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();

    /// <summary>Activity log entries for this board.</summary>
    public ICollection<BoardActivity> Activities { get; set; } = new List<BoardActivity>();

    /// <summary>Planning poker sessions on this board's cards.</summary>
    public ICollection<PokerSession> PokerSessions { get; set; } = new List<PokerSession>();
}
