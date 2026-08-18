using DotNetCloud.Core.DTOs;

namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Receives real-time notification events for the web UI.
/// </summary>
public interface IRealtimeNotificationClient
{
    /// <summary>Raised when a new notification is created for the current user.</summary>
    event Action<NotificationDto>? NotificationCreated;

    /// <summary>
    /// Starts the real-time connection. Safe to call multiple times; no-ops if
    /// already started or if no auth cookie is available.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
}
