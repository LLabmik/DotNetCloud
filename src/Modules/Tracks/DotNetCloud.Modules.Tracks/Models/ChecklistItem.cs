namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a single item within a <see cref="Checklist"/>.
/// </summary>
public sealed class ChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChecklistId { get; set; }
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }
    public double Position { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Checklist? Checklist { get; set; }
}
