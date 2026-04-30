using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A rule that automatically creates work items on a schedule (cron expression).
/// For example: weekly standup notes, monthly reports, daily checklists.
/// </summary>
public sealed class RecurringRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid SwimlaneId { get; set; }
    public WorkItemType Type { get; set; } = WorkItemType.Item;
    /// <summary>JSON template with default fields: title, description, priority, labels, assignee, story points.</summary>
    public required string TemplateJson { get; set; }
    public required string CronExpression { get; set; }
    public DateTime NextRunAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public Swimlane? Swimlane { get; set; }
}
