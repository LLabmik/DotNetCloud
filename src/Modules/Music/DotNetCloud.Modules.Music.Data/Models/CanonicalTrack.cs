namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Canonical (shared) track — intrinsic audio properties stored once per ContentHash.
/// No OwnerId — shared across all users who index the same file.
/// </summary>
public sealed class CanonicalTrack
{
    /// <summary>SHA-256 content hash of the underlying audio file (primary key).</summary>
    public required string ContentHash { get; set; }

    /// <summary>Track title.</summary>
    public required string Title { get; set; }

    /// <summary>Track number on the album.</summary>
    public int? TrackNumber { get; set; }

    /// <summary>Disc number.</summary>
    public int? DiscNumber { get; set; }

    /// <summary>Track duration in ticks.</summary>
    public long DurationTicks { get; set; }

    /// <summary>Audio bitrate in bps.</summary>
    public long? Bitrate { get; set; }

    /// <summary>Sample rate in Hz.</summary>
    public int? SampleRate { get; set; }

    /// <summary>Number of audio channels.</summary>
    public int? Channels { get; set; }

    /// <summary>MIME type (e.g. "audio/flac").</summary>
    public required string MimeType { get; set; }

    /// <summary>Release year.</summary>
    public int? Year { get; set; }

    /// <summary>MusicBrainz recording identifier.</summary>
    public string? MusicBrainzRecordingId { get; set; }

    /// <summary>ISRC (International Standard Recording Code).</summary>
    public string? Isrc { get; set; }

    /// <summary>Beats per minute.</summary>
    public int? Bpm { get; set; }

    /// <summary>Composers (semicolon-separated for multiple).</summary>
    public string? Composers { get; set; }

    /// <summary>When the canonical track record was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the canonical track record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: user tracks referencing this canonical track.</summary>
    public ICollection<UserTrack> UserTracks { get; set; } = new List<UserTrack>();

    /// <summary>Artist associations for this canonical track.</summary>
    public ICollection<CanonicalTrackArtist> TrackArtists { get; set; } = new List<CanonicalTrackArtist>();

    /// <summary>Genre associations for this canonical track.</summary>
    public ICollection<CanonicalTrackGenre> TrackGenres { get; set; } = new List<CanonicalTrackGenre>();
}
