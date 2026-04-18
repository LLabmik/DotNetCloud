using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a time-boxed sprint iteration on a board.
/// </summary>
public sealed class Sprint
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this sprint belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Sprint title (e.g., "Sprint 1", "March Iteration").</summary>
    public required string Title { get; set; }

    /// <summary>Optional sprint goal description.</summary>
    public string? Goal { get; set; }

    /// <summary>Sprint start date (UTC).</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Sprint end date (UTC).</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Current sprint status.</summary>
    public SprintStatus Status { get; set; } = SprintStatus.Planning;

    /// <summary>Target story points for capacity planning.</summary>
    public int? TargetStoryPoints { get; set; }

    /// <summary>Duration of this sprint in weeks (1–16). Used by the planning wizard.</summary>
    public int? DurationWeeks { get; set; }

    /// <summary>Planned order within the year plan (1-based sequential).</summary>
    public int? PlannedOrder { get; set; }

    /// <summary>Timestamp when the sprint was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the sprint was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }

    /// <summary>Cards assigned to this sprint.</summary>
    public ICollection<SprintCard> SprintCards { get; set; } = new List<SprintCard>();
}
