namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Defines whether a work item can transition from one swimlane to another.
/// Used to enforce workflow rules (e.g., prevent skipping from Backlog directly to Done).
/// </summary>
public sealed class SwimlaneTransitionRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid FromSwimlaneId { get; set; }
    public Guid ToSwimlaneId { get; set; }
    public bool IsAllowed { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product? Product { get; set; }
    public Swimlane? FromSwimlane { get; set; }
    public Swimlane? ToSwimlane { get; set; }
}
