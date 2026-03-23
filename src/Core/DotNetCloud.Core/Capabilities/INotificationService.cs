using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Sends user-facing notifications through configured channels.
/// </summary>
/// <remarks>
/// <para><b>Capability tier:</b> Public — automatically granted to all modules.</para>
/// <para>
/// Modules publish notifications when user-visible events occur (shares, invitations,
/// reminders, mentions). The notification service routes them to the target user
/// through available channels (in-app, push, email).
/// </para>
/// </remarks>
public interface INotificationService : ICapabilityInterface
{
    /// <summary>
    /// Sends a notification to a specific user.
    /// </summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="notification">Notification details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the same notification to multiple users.
    /// </summary>
    /// <param name="userIds">Target user IDs.</param>
    /// <param name="notification">Notification details (a copy is sent to each user).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendToManyAsync(IEnumerable<Guid> userIds, NotificationDto notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread notifications for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<NotificationDto>> GetUnreadAsync(Guid userId, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="userId">The user ID (must own the notification).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all unread notifications as read for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
