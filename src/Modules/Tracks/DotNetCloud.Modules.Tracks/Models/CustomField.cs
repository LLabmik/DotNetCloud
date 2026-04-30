using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A custom field definition for a Product. Product admins can define additional
/// fields on work items — text, number, date, single/multi-select, or user picker.
/// </summary>
public sealed class CustomField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Name { get; set; }
    public CustomFieldType Type { get; set; } = CustomFieldType.Text;
    /// <summary>JSON array of select options (for SingleSelect / MultiSelect types).</summary>
    public string? OptionsJson { get; set; }
    public bool IsRequired { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public ICollection<WorkItemFieldValue> FieldValues { get; set; } = new List<WorkItemFieldValue>();
}
