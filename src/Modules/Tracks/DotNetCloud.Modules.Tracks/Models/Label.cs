namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A color-coded label that can be applied to work items within a Product.
/// </summary>
public sealed class Label
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Title { get; set; }
    public required string Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ICollection<WorkItemLabel> WorkItemLabels { get; set; } = new List<WorkItemLabel>();
}
