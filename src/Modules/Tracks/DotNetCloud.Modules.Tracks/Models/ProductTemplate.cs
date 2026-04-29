namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A reusable template for creating Products with predefined swimlanes and settings.
/// </summary>
public sealed class ProductTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsBuiltIn { get; set; }
    public Guid CreatedByUserId { get; set; }
    public required string DefinitionJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
