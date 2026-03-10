namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Delivers desktop notifications to the OS notification system
/// (Windows balloon tip / Linux libnotify).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Optional callback fired when the user activates a notification action.
    /// The callback parameter is the action URL supplied at show-time.
    /// </summary>
    Action<string>? OnNotificationActivated { get; set; }

    /// <summary>
    /// Shows a desktop notification with the given <paramref name="title"/>,
    /// <paramref name="body"/>, and optional severity <paramref name="type"/>.
    /// </summary>
    /// <param name="title">Short notification title.</param>
    /// <param name="body">Notification body text.</param>
    /// <param name="type">Severity classification (controls icon / urgency).</param>
    /// <param name="actionUrl">Optional URL opened when notification is activated.</param>
    void ShowNotification(string title, string body, NotificationType type = NotificationType.Info, string? actionUrl = null);
}
