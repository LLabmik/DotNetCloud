using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends a real-time SignalR event to the recipient so connected clients
/// (e.g. the notification bell) can refresh their unread badge immediately.
/// </summary>
internal sealed class RealtimeNotificationChannel : INotificationChannel
{
    private readonly IRealtimeBroadcaster _broadcaster;

    public RealtimeNotificationChannel(IRealtimeBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    /// <inheritdoc />
    public Task DeliverAsync(NotificationDto notification, CancellationToken ct = default)
    {
        return _broadcaster.SendToUserAsync(
            notification.UserId,
            "notification.created",
            notification,
            ct);
    }
}
