namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Records track playback history.
/// </summary>
public sealed class PlaybackHistory
{
    /// <summary>Unique identifier for this record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who played the track.</summary>
    public Guid UserId { get; set; }

    /// <summary>The track that was played.</summary>
    public Guid TrackId { get; set; }

    /// <summary>When playback started (UTC).</summary>
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Duration played in seconds.</summary>
    public int DurationPlayedSeconds { get; set; }

    /// <summary>Navigation to the track.</summary>
    public Track? Track { get; set; }
}
