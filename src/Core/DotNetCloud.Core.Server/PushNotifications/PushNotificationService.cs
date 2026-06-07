namespace DotNetCloud.Core.Server.PushNotifications;

/// <summary>
/// Sends push notifications to user devices.
/// Core.Server uses a no-op implementation; real push delivery is handled
/// by the Chat module host via gRPC.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>Sends a notification to a specific user.</summary>
    Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// A push notification payload.
/// </summary>
public sealed record PushNotification
{
    /// <summary>Notification title.</summary>
    public required string Title { get; init; }

    /// <summary>Notification body text.</summary>
    public required string Body { get; init; }

    /// <summary>Optional image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Custom data payload for the client app.</summary>
    public Dictionary<string, string> Data { get; init; } = [];

    /// <summary>Notification category.</summary>
    public NotificationCategory Category { get; init; } = NotificationCategory.System;
}

/// <summary>
/// Notification category.
/// </summary>
public enum NotificationCategory
{
    /// <summary>General system notification.</summary>
    System,

    /// <summary>New message notification.</summary>
    Message,

    /// <summary>File share notification.</summary>
    FileShare,

    /// <summary>File shared with user.</summary>
    FileShared,

    /// <summary>Public link accessed.</summary>
    PublicLinkAccessed,

    /// <summary>Share about to expire.</summary>
    ShareExpiring,

    /// <summary>Resource shared with user.</summary>
    ResourceShared,

    /// <summary>Storage quota warning.</summary>
    QuotaWarning,

    /// <summary>Storage quota critical.</summary>
    QuotaCritical,

    /// <summary>Reminder notification.</summary>
    Reminder,

    /// <summary>Chat mention notification.</summary>
    Mention,

    /// <summary>Video call notification.</summary>
    Call,
}
