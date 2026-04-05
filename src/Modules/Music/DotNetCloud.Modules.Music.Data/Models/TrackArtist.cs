namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Junction table for track-artist many-to-many relationships.
/// Handles multi-artist tracks (e.g. features, collaborations).
/// </summary>
public sealed class TrackArtist
{
    /// <summary>The track ID.</summary>
    public Guid TrackId { get; set; }

    /// <summary>The artist ID.</summary>
    public Guid ArtistId { get; set; }

    /// <summary>Whether this is the primary artist for the track.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Navigation to the track.</summary>
    public Track? Track { get; set; }

    /// <summary>Navigation to the artist.</summary>
    public Artist? Artist { get; set; }
}
