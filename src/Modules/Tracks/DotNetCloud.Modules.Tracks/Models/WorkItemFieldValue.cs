namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// The value of a custom field on a specific work item.
/// </summary>
public sealed class WorkItemFieldValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid CustomFieldId { get; set; }
    /// <summary>The field value stored as a string (parsed/validated by the service layer).</summary>
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
    public CustomField? CustomField { get; set; }
}
