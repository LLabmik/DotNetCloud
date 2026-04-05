namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Junction table for playlist-track ordered relationships.
/// </summary>
public sealed class PlaylistTrack
{
    /// <summary>The playlist ID.</summary>
    public Guid PlaylistId { get; set; }

    /// <summary>The track ID.</summary>
    public Guid TrackId { get; set; }

    /// <summary>Sort order within the playlist (0-based).</summary>
    public int SortOrder { get; set; }

    /// <summary>When the track was added to the playlist (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the playlist.</summary>
    public Playlist? Playlist { get; set; }

    /// <summary>Navigation to the track.</summary>
    public Track? Track { get; set; }
}
