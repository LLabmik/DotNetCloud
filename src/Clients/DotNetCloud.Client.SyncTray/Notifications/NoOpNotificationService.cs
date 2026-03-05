namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Silent fallback <see cref="INotificationService"/> used on platforms that
/// have no native notification mechanism.
/// </summary>
internal sealed class NoOpNotificationService : INotificationService
{
    /// <inheritdoc/>
    public void ShowNotification(string title, string body, NotificationType type = NotificationType.Info)
    {
        // No-op: platform has no notification support.
    }
}
