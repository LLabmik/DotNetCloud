namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Top-level Tracks container belonging to a Core Organization.
/// Replaces Board as the root project management entity.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public Guid OwnerId { get; set; }
    public bool SubItemsEnabled { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Swimlane> Swimlanes { get; set; } = new List<Swimlane>();
    public ICollection<ProductMember> Members { get; set; } = new List<ProductMember>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<CustomField> CustomFields { get; set; } = new List<CustomField>();
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<RecurringRule> RecurringRules { get; set; } = new List<RecurringRule>();
}
