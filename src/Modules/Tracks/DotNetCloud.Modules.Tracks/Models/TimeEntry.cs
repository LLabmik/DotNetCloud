namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A time tracking entry logged against a work item.
/// </summary>
public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
}
