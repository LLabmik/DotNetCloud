namespace DotNetCloud.Client.Core;

/// <summary>
/// Repeat mode for music playback.
/// </summary>
public enum RepeatMode
{
    /// <summary>No repeat — playback stops at end of queue.</summary>
    Off = 0,

    /// <summary>Repeat the current track.</summary>
    One = 1,

    /// <summary>Repeat the entire queue.</summary>
    All = 2,
}
