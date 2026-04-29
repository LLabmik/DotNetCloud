namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A comment on a work item.
/// </summary>
public sealed class WorkItemComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid UserId { get; set; }
    public required string Content { get; set; }
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
}
