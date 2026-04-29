namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Many-to-many join between Sprint and WorkItem (Item type only).
/// </summary>
public sealed class SprintItem
{
    public Guid SprintId { get; set; }
    public Guid ItemId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Sprint? Sprint { get; set; }
    public WorkItem? Item { get; set; }
}
