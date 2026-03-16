namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Sends push notifications to user devices via FCM or UnifiedPush.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>Sends a notification to a specific user's registered devices.</summary>
    Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Sends a notification to multiple users.</summary>
    Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>Registers a device for push notifications.</summary>
    Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>Unregisters a device.</summary>
    Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default);
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
/// Device registration for push notifications.
/// </summary>
public sealed record DeviceRegistration
{
    /// <summary>Device token (FCM token or UnifiedPush endpoint).</summary>
    public required string Token { get; init; }

    /// <summary>Push provider type.</summary>
    public PushProvider Provider { get; init; }

    /// <summary>UnifiedPush distributor endpoint URL (only for UnifiedPush).</summary>
    public string? Endpoint { get; init; }
}

/// <summary>
/// Push notification provider types.
/// </summary>
public enum PushProvider
{
    /// <summary>Firebase Cloud Messaging.</summary>
    FCM,

    /// <summary>UnifiedPush (open protocol).</summary>
    UnifiedPush
}

/// <summary>
/// Categories of push notifications.
/// </summary>
public enum NotificationCategory
{
    /// <summary>New chat message.</summary>
    ChatMessage,

    /// <summary>User was @mentioned.</summary>
    ChatMention,

    /// <summary>New announcement.</summary>
    Announcement,

    /// <summary>File shared with user.</summary>
    FileShared,

    /// <summary>Storage quota warning (approaching limit).</summary>
    QuotaWarning,

    /// <summary>Storage quota critical (near limit).</summary>
    QuotaCritical,

    /// <summary>System notification.</summary>
    System
}
