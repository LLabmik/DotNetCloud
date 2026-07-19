namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Tracks whether the Android app is currently in the foreground (visible to the user).
/// Used to suppress notifications when the user is actively using the app.
/// </summary>
public interface IAppForegroundService
{
    /// <summary><c>true</c> when the app is visible to the user (at least one Activity is resumed).</summary>
    bool IsInForeground { get; }

    /// <summary>Raised when the foreground state changes.</summary>
    event EventHandler<bool>? ForegroundChanged;

    /// <summary>
    /// Called by MainActivity.OnResume and MainActivity.OnPause.
    /// </summary>
    void SetForeground(bool isInForeground);
}
