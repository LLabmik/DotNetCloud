namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Plays the in-app alert sound for a new chat message.
/// </summary>
/// <remarks>
/// Used while the app is visible, where <see cref="ChatAlertPolicy"/> deliberately produces no
/// system notification; the platform implementation is <c>AndroidChatSoundPlayer</c>.
/// Implementations must never throw and must be safe to call from any thread — a failure to play
/// a sound must never affect message handling.
/// </remarks>
public interface IChatSoundPlayer
{
    /// <summary>
    /// Whether the user currently has the in-app chat sound enabled
    /// (<see cref="ChatSoundSettings.PreferenceKey"/>).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Loads the alert sound into memory so the first ding during this process is not swallowed
    /// while the platform decodes the resource. Safe to call repeatedly.
    /// </summary>
    void Prepare();

    /// <summary>
    /// Plays the alert sound once, unless the user has it disabled.
    /// </summary>
    void PlayMessageAlert();
}
