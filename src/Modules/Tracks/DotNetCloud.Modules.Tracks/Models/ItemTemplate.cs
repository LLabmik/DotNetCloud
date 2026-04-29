using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A reusable template for creating WorkItems with predefined fields.
/// </summary>
public sealed class ItemTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Name { get; set; }
    public string? TitlePattern { get; set; }
    public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.None;
    public string? LabelIdsJson { get; set; }
    public string? ChecklistsJson { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
