using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Delivers a notification as a device push via the Chat module (FCM/UnifiedPush).
/// Preference checks (push enabled, DND, muted channels, presence) happen inside
/// the Chat module's NotificationRouter — they are NOT duplicated here.
/// </summary>
internal sealed class PushNotificationChannel : INotificationChannel
{
    private readonly IChatApiClient _chatApiClient;

    public PushNotificationChannel(IChatApiClient chatApiClient)
    {
        _chatApiClient = chatApiClient;
    }

    /// <inheritdoc />
    public async Task DeliverAsync(NotificationDto notification, CancellationToken ct = default)
    {
        await _chatApiClient.SendPushNotificationAsync(
            notification.UserId,
            notification.Title,
            notification.Message ?? string.Empty,
            MapCategory(notification),
            BuildData(notification),
            ct);
    }

    private static string MapCategory(NotificationDto n) => n.Type switch
    {
        NotificationType.Share => n.SourceModuleId == "dotnetcloud.files" ? "FileShared" : "ResourceShared",
        NotificationType.Mention => "Mention",
        NotificationType.Reminder => "Reminder",
        NotificationType.Invitation => "CalendarInvitation",
        _ => "System"
    };

    private static Dictionary<string, string> BuildData(NotificationDto n) => new()
    {
        ["actionUrl"] = n.ActionUrl ?? string.Empty,
        ["type"] = n.Type.ToString(),
        ["sourceModuleId"] = n.SourceModuleId
    };
}
