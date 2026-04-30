namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A goal or key result (OKR). Objectives can have nested key results via ParentGoalId.
/// Progress can be tracked manually or automatically from linked work items.
/// </summary>
public sealed class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>"objective" or "key_result"</summary>
    public string Type { get; set; } = "objective";

    /// <summary>For key results, the parent objective ID.</summary>
    public Guid? ParentGoalId { get; set; }

    public double? TargetValue { get; set; }
    public double? CurrentValue { get; set; }

    /// <summary>"manual" or "automatic"</summary>
    public string ProgressType { get; set; } = "manual";

    /// <summary>NotStarted, OnTrack, AtRisk, Behind, Completed</summary>
    public string Status { get; set; } = "not_started";

    public DateTime? DueDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product? Product { get; set; }
    public Goal? ParentGoal { get; set; }
    public ICollection<Goal> ChildGoals { get; set; } = new List<Goal>();
    public ICollection<GoalWorkItem> LinkedWorkItems { get; set; } = new List<GoalWorkItem>();
}

/// <summary>
/// Junction entity linking a work item to a goal/key result.
/// </summary>
public sealed class GoalWorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public Guid WorkItemId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Goal? Goal { get; set; }
    public WorkItem? WorkItem { get; set; }
}
