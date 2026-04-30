namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A shareable link that grants external access to a work item with limited permissions.
/// Links can be configured with an expiry date and View or Comment permission level.
/// </summary>
public sealed class WorkItemShareLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid CreatedByUserId { get; set; }

    /// <summary>Random URL-safe token used in the share URL.</summary>
    public required string Token { get; set; }

    /// <summary>Permission level granted by this link.</summary>
    public SharePermission Permission { get; set; } = SharePermission.View;

    /// <summary>Optional expiry. When null, the link never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether this link is active. Can be revoked.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
}

/// <summary>
/// Permission level for a shared work item link.
/// </summary>
public enum SharePermission
{
    /// <summary>Can only view the work item and its comments.</summary>
    View = 0,

    /// <summary>Can view and add comments.</summary>
    Comment = 1
}
