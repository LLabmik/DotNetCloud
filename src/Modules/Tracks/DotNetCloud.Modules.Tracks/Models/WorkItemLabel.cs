namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Many-to-many join between WorkItem and Label.
/// </summary>
public sealed class WorkItemLabel
{
    public Guid WorkItemId { get; set; }
    public Guid LabelId { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
    public Label? Label { get; set; }
}
