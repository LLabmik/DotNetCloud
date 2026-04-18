namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Junction table for track-genre many-to-many relationships.
/// </summary>
public sealed class TrackGenre
{
    /// <summary>The track ID.</summary>
    public Guid TrackId { get; set; }

    /// <summary>The genre ID.</summary>
    public Guid GenreId { get; set; }

    /// <summary>Navigation to the track.</summary>
    public Track? Track { get; set; }

    /// <summary>Navigation to the genre.</summary>
    public Genre? Genre { get; set; }
}
