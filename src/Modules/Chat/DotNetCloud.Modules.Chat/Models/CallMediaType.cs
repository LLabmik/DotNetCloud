namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Defines the type of media in a video call.
/// </summary>
public enum CallMediaType
{
    /// <summary>Audio-only call.</summary>
    Audio,

    /// <summary>Video call (includes audio).</summary>
    Video,

    /// <summary>Screen sharing stream.</summary>
    ScreenShare
}
