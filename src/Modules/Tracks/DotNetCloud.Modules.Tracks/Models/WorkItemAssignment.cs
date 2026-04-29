namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// User assignment to a work item.
/// </summary>
public sealed class WorkItemAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
}
