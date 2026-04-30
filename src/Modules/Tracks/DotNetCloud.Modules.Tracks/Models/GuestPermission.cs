namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Per-work-item permission granted to a guest user.
/// Defines what level of access a guest has on a specific work item.
/// </summary>
public sealed class GuestPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuestUserId { get; set; }
    public Guid WorkItemId { get; set; }

    /// <summary>Permission level for this work item.</summary>
    public GuestPermissionLevel Permission { get; set; } = GuestPermissionLevel.View;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GuestUser? GuestUser { get; set; }
    public WorkItem? WorkItem { get; set; }
}

/// <summary>
/// Permission level for a guest on a specific work item.
/// </summary>
public enum GuestPermissionLevel
{
    /// <summary>Can only view the work item.</summary>
    View = 0,

    /// <summary>Can view and comment on the work item.</summary>
    Comment = 1
}
