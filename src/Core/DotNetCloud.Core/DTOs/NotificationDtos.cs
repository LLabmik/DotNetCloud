namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a user-facing notification.
/// </summary>
public sealed record NotificationDto
{
    /// <summary>
    /// Unique identifier for the notification.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The user ID this notification targets.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Module that generated this notification.
    /// </summary>
    public required string SourceModuleId { get; init; }

    /// <summary>
    /// Notification type (determines icon and routing behavior).
    /// </summary>
    public required NotificationType Type { get; init; }

    /// <summary>
    /// Short summary shown in notification lists and toasts.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional detailed message body.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Priority level affecting delivery urgency.
    /// </summary>
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>
    /// Optional deep-link URL within the application for click-through navigation.
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// The entity type that caused this notification (for grouping).
    /// </summary>
    public CrossModuleLinkType? RelatedEntityType { get; init; }

    /// <summary>
    /// The entity ID that caused this notification.
    /// </summary>
    public Guid? RelatedEntityId { get; init; }

    /// <summary>
    /// When the notification was created (UTC).
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When the notification was read by the user, or null if unread.
    /// </summary>
    public DateTime? ReadAtUtc { get; init; }

    /// <summary>
    /// Whether the notification has been read.
    /// </summary>
    public bool IsRead => ReadAtUtc.HasValue;
}

/// <summary>
/// Type of notification for UI presentation and routing.
/// </summary>
public enum NotificationType
{
    /// <summary>Generic informational notification.</summary>
    Info,

    /// <summary>A resource was shared with the user.</summary>
    Share,

    /// <summary>A calendar event invitation.</summary>
    Invitation,

    /// <summary>A calendar or task reminder.</summary>
    Reminder,

    /// <summary>The user was mentioned in a note, comment, or chat.</summary>
    Mention,

    /// <summary>A resource the user owns or follows was updated.</summary>
    Update,

    /// <summary>A system-level alert (quota, maintenance, etc.).</summary>
    SystemAlert
}

/// <summary>
/// Priority level for notification delivery.
/// </summary>
public enum NotificationPriority
{
    /// <summary>Delivered at next poll or batch interval.</summary>
    Low,

    /// <summary>Default — delivered promptly via available channels.</summary>
    Normal,

    /// <summary>Delivered immediately via push/realtime if possible.</summary>
    High,

    /// <summary>Critical alert — delivered via all available channels.</summary>
    Urgent
}
