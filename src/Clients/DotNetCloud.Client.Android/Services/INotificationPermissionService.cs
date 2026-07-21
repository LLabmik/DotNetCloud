namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Checks and prompts the user to grant <c>POST_NOTIFICATIONS</c> permission
/// (Android 13+) so that calendar reminders and other notifications are displayed.
/// </summary>
public interface INotificationPermissionService
{
    /// <summary>Returns <c>true</c> when the app has notification posting permission.</summary>
    bool HasNotificationPermission();

    /// <summary>
    /// Opens the system app notification settings page where the user can
    /// enable notifications for this app.
    /// </summary>
    void OpenNotificationSettings();
}
