namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a user who is "watching" a work item.
/// Watchers get notified when the work item is updated, commented on, or changed.
/// This is similar to "subscribing" to a ticket — you'll get updates without being assigned.
/// </summary>
public sealed class WorkItemWatcher
{
    /// <summary>The work item being watched.</summary>
    public Guid WorkItemId { get; set; }

    /// <summary>The user who is watching.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the user started watching this item.</summary>
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public WorkItem? WorkItem { get; set; }
}
