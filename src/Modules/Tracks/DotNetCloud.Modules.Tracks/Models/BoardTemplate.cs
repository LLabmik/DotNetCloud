namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a board template for creating pre-configured boards with lists, labels, and default cards.
/// </summary>
public sealed class BoardTemplate
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Template name (e.g., "Kanban", "Scrum", "Bug Tracking").</summary>
    public required string Name { get; set; }

    /// <summary>Optional description of the template.</summary>
    public string? Description { get; set; }

    /// <summary>Category for grouping templates (e.g., "Development", "Personal").</summary>
    public string? Category { get; set; }

    /// <summary>Whether this is a built-in system template.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>The user who created this template. Null for built-in templates.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>JSON-serialized template definition containing lists, labels, and default cards.</summary>
    public required string DefinitionJson { get; set; }

    /// <summary>Timestamp when the template was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the template was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
