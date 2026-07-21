namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Checks and prompts the user to grant <c>SCHEDULE_EXACT_ALARM</c> permission
/// so that calendar reminders fire at the exact scheduled time (not inexact with a window).
/// </summary>
public interface IExactAlarmPermissionService
{
    /// <summary>Returns <c>true</c> when the app has exact alarm scheduling permission.</summary>
    bool HasExactAlarmPermission();

    /// <summary>
    /// Opens the system settings page where the user can grant
    /// <c>SCHEDULE_EXACT_ALARM</c> permission for this app.
    /// </summary>
    void OpenPermissionSettings();
}
