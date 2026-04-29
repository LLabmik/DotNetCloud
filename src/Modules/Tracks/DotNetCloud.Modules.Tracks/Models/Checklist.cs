namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A checklist on an Item-level WorkItem. Only used when Product.SubItemsEnabled is false.
/// </summary>
public sealed class Checklist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public required string Title { get; set; }
    public double Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? Item { get; set; }
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}
