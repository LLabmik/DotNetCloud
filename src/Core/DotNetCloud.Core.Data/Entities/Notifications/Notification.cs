using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Data.Entities.Notifications;

/// <summary>
/// Persisted in-app notification for a user.
/// </summary>
public sealed class Notification
{
    /// <summary>Unique identifier for this notification.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user ID this notification targets.</summary>
    public Guid UserId { get; set; }

    /// <summary>Module that generated this notification.</summary>
    public string SourceModuleId { get; set; } = string.Empty;

    /// <summary>Notification type (determines icon and routing).</summary>
    public NotificationType Type { get; set; }

    /// <summary>Short summary shown in notification lists and toasts.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional detailed message body.</summary>
    public string? Message { get; set; }

    /// <summary>Priority level affecting delivery urgency.</summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    /// <summary>Optional deep-link URL within the application.</summary>
    public string? ActionUrl { get; set; }

    /// <summary>The entity type that caused this notification.</summary>
    public CrossModuleLinkType? RelatedEntityType { get; set; }

    /// <summary>The entity ID that caused this notification.</summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>When the notification was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the notification was read by the user, or null if unread.</summary>
    public DateTime? ReadAtUtc { get; set; }
}
