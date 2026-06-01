using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Records a scrobble event (track play completion for last.fm-style history).
/// </summary>
public sealed class ScrobbleRecord
{
    /// <summary>Unique identifier for this scrobble.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who scrobbled.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user track that was scrobbled.</summary>
    public Guid UserTrackId { get; set; }

    /// <summary>Artist name at time of scrobble.</summary>
    public required string ArtistName { get; set; }

    /// <summary>Track title at time of scrobble.</summary>
    public required string TrackTitle { get; set; }

    /// <summary>Album title at time of scrobble.</summary>
    public string? AlbumTitle { get; set; }

    /// <summary>When the scrobble occurred (UTC).</summary>
    public DateTime ScrobbledAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the user track.</summary>
    public UserTrack? UserTrack { get; set; }
}
