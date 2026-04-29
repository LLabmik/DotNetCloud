using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Dependency relationship between two work items.
/// </summary>
public sealed class WorkItemDependency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid DependsOnWorkItemId { get; set; }
    public DependencyType Type { get; set; } = DependencyType.BlockedBy;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
    public WorkItem? DependsOnWorkItem { get; set; }
}
