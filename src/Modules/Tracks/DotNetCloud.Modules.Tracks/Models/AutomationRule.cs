using System.Text.Json;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// An automation rule that evaluates triggers and conditions, then executes actions.
/// "When X happens and Y is true, do Z."
/// </summary>
public sealed class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// Trigger type: work_item_created, work_item_moved, status_changed, due_date_approaching, assigned.
    /// </summary>
    public required string Trigger { get; set; }

    /// <summary>
    /// JSON array of condition objects. Each condition: { "field": "...", "operator": "...", "value": "..." }
    /// </summary>
    public string ConditionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of action objects. Each action: { "type": "...", "parameters": { ... } }
    /// </summary>
    public string ActionsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product? Product { get; set; }
}
