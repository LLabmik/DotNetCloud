namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Dispatches queued notifications without recursively re-enqueueing.
/// </summary>
internal interface IQueuedNotificationDispatcher
{
    /// <summary>
    /// Attempts delivery for a queued notification.
    /// </summary>
    /// <returns><c>true</c> when delivered; otherwise <c>false</c>.</returns>
    Task<bool> DispatchQueuedAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default);
}
