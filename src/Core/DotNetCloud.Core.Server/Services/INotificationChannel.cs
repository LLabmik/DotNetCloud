using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// A delivery channel for persisted notifications (real-time, push, email, ...).
/// </summary>
public interface INotificationChannel
{
    /// <summary>Delivers a notification through this channel.</summary>
    Task DeliverAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}
