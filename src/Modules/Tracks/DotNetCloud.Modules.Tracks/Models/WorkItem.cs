using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Unified work item for all hierarchy levels: Epic, Feature, Item, SubItem.
/// </summary>
public sealed class WorkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid? ParentWorkItemId { get; set; }
    public WorkItemType Type { get; set; }
    public Guid? SwimlaneId { get; set; }
    public int ItemNumber { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public double Position { get; set; }
    public Priority Priority { get; set; } = Priority.None;
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int? StoryPoints { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public WorkItem? ParentWorkItem { get; set; }
    public ICollection<WorkItem> ChildWorkItems { get; set; } = new List<WorkItem>();
    public Swimlane? Swimlane { get; set; }
    public ICollection<WorkItemAssignment> Assignments { get; set; } = new List<WorkItemAssignment>();
    public ICollection<WorkItemLabel> WorkItemLabels { get; set; } = new List<WorkItemLabel>();
    public ICollection<WorkItemComment> Comments { get; set; } = new List<WorkItemComment>();
    public ICollection<WorkItemAttachment> Attachments { get; set; } = new List<WorkItemAttachment>();
    public ICollection<WorkItemDependency> Dependencies { get; set; } = new List<WorkItemDependency>();
    public ICollection<WorkItemDependency> Dependents { get; set; } = new List<WorkItemDependency>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<SprintItem> SprintItems { get; set; } = new List<SprintItem>();
    public ICollection<Checklist> Checklists { get; set; } = new List<Checklist>();
    public ICollection<PokerSession> PokerSessions { get; set; } = new List<PokerSession>();
    public ICollection<WorkItemWatcher> Watchers { get; set; } = new List<WorkItemWatcher>();
    public Guid? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }
    public ICollection<WorkItemFieldValue> FieldValues { get; set; } = new List<WorkItemFieldValue>();
    public Guid? RecurringRuleId { get; set; }
    public RecurringRule? RecurringRule { get; set; }
}
