namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Stores user music preferences.
/// </summary>
public sealed class UserMusicPreference
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Selected EQ preset ID (null for none).</summary>
    public Guid? ActiveEqPresetId { get; set; }

    /// <summary>Preferred playback volume (0.0–1.0).</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Whether shuffle is enabled.</summary>
    public bool ShuffleEnabled { get; set; }

    /// <summary>Repeat mode: None, One, All.</summary>
    public RepeatMode RepeatMode { get; set; }

    /// <summary>When the preference was last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the active EQ preset.</summary>
    public EqPreset? ActiveEqPreset { get; set; }
}

/// <summary>
/// Repeat mode for playback.
/// </summary>
public enum RepeatMode
{
    /// <summary>No repeat.</summary>
    None,

    /// <summary>Repeat the current track.</summary>
    One,

    /// <summary>Repeat the entire queue/playlist.</summary>
    All
}
