namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A saved filter/view configuration for a product.
/// Users can save their current filter/sort/group state as a named view
/// and quickly switch between them.
/// </summary>
public sealed class CustomView
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The product this view belongs to.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The user who created this view (owner).</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name for the saved view.</summary>
    public required string Name { get; set; }

    /// <summary>JSON-serialized filter state.</summary>
    public string FilterJson { get; set; } = "{}";

    /// <summary>JSON-serialized sort state.</summary>
    public string SortJson { get; set; } = "{}";

    /// <summary>Group by field (e.g., "Assignee", "Priority", "None").</summary>
    public string? GroupBy { get; set; }

    /// <summary>Layout type: Kanban, Backlog, List, Calendar, Timeline.</summary>
    public string Layout { get; set; } = "Kanban";

    /// <summary>Whether this view is shared with all product members.</summary>
    public bool IsShared { get; set; }

    /// <summary>When this view was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When this view was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>The product navigation property.</summary>
    public Product? Product { get; set; }
}
