using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A time-boxed sprint iteration scoped to an Epic.
/// </summary>
public sealed class Sprint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EpicId { get; set; }
    public required string Title { get; set; }
    public string? Goal { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planning;
    public int? TargetStoryPoints { get; set; }
    public int? DurationWeeks { get; set; }
    public int? PlannedOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? Epic { get; set; }
    public ICollection<SprintItem> SprintItems { get; set; } = new List<SprintItem>();
}
