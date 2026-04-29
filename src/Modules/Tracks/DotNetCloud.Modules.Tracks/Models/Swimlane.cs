using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Unified kanban column. Can belong to a Product (containing Epics) or a WorkItem (Epic containing Features, Feature containing Items).
/// </summary>
public sealed class Swimlane
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SwimlaneContainerType ContainerType { get; set; }
    public Guid ContainerId { get; set; }
    public required string Title { get; set; }
    public string? Color { get; set; }
    public double Position { get; set; }
    public int? CardLimit { get; set; }
    public bool IsDone { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
