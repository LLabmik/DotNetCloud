namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Shared identifiers for the in-app chat alert ("ding") preference.
/// </summary>
/// <remarks>
/// Stored locally through <see cref="IAppPreferences"/>. The Blazor client keeps the equivalent
/// setting server-side (module <c>dotnetcloud.chat</c>, key <c>message-sound-enabled</c>);
/// unifying the two through <c>api/v1/core/user-settings</c> is a separate follow-up.
/// </remarks>
public static class ChatSoundSettings
{
    /// <summary>Preference key holding whether the in-app chat ding is enabled.</summary>
    public const string PreferenceKey = "chat_message_sound_enabled";

    /// <summary>Default value of the in-app chat ding preference (enabled).</summary>
    public const bool DefaultEnabled = true;
}
