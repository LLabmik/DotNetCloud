namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Silent fallback <see cref="INotificationService"/> used on platforms that
/// have no native notification mechanism.
/// </summary>
internal sealed class NoOpNotificationService : INotificationService
{
    /// <inheritdoc/>
    public Action<string>? OnNotificationActivated { get; set; }

    /// <inheritdoc/>
    public void ShowNotification(
        string title,
        string body,
        NotificationType type = NotificationType.Info,
        string? actionUrl = null,
        string? groupKey = null,
        string? replaceKey = null)
    {
        // No-op: platform has no notification support.
        _ = actionUrl;
        _ = groupKey;
        _ = replaceKey;
    }
}
