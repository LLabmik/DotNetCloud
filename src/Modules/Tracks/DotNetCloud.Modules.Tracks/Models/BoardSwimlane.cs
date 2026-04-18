namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a column (swimlane) within a board. Cards are organized into swimlanes.
/// </summary>
public sealed class BoardSwimlane
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this swimlane belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Swimlane title (e.g., "To Do", "In Progress", "Done").</summary>
    public required string Title { get; set; }

    /// <summary>Position within the board for ordering. Uses gap-based positioning.</summary>
    public double Position { get; set; }

    /// <summary>Hex color code for UI display.</summary>
    public string? Color { get; set; }

    /// <summary>Work-in-progress limit. Null means no limit.</summary>
    public int? CardLimit { get; set; }

    /// <summary>Whether this is a "done" column — cards here count as completed for sprint tracking.</summary>
    public bool IsDone { get; set; }

    /// <summary>Whether the swimlane has been archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Timestamp when the swimlane was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the swimlane was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }

    /// <summary>Cards in this swimlane.</summary>
    public ICollection<Card> Cards { get; set; } = new List<Card>();
}
