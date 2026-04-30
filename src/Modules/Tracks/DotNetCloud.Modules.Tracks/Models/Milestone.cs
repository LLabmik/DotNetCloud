using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A key date marker on the product timeline — ship dates, review checkpoints,
/// phase completions. Work items can be assigned to a milestone.
/// </summary>
public sealed class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Upcoming;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
