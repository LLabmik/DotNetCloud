namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents a music track. Links to a FileNode in the Files module.
/// </summary>
public sealed class Track
{
    /// <summary>Unique identifier for this track.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The FileNode ID this track references (from Files module).</summary>
    public Guid FileNodeId { get; set; }

    /// <summary>The user who owns this track record.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Track title.</summary>
    public required string Title { get; set; }

    /// <summary>Track number on the album.</summary>
    public int? TrackNumber { get; set; }

    /// <summary>Disc number.</summary>
    public int? DiscNumber { get; set; }

    /// <summary>Track duration in ticks.</summary>
    public long DurationTicks { get; set; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Audio bitrate in bps.</summary>
    public long? Bitrate { get; set; }

    /// <summary>Sample rate in Hz.</summary>
    public int? SampleRate { get; set; }

    /// <summary>Number of audio channels.</summary>
    public int? Channels { get; set; }

    /// <summary>MIME type (e.g. "audio/flac").</summary>
    public required string MimeType { get; set; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; set; }

    /// <summary>Album ID this track belongs to.</summary>
    public Guid? AlbumId { get; set; }

    /// <summary>Release year.</summary>
    public int? Year { get; set; }

    /// <summary>Play count for this track.</summary>
    public int PlayCount { get; set; }

    /// <summary>Whether the track has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Timestamp when soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the track was added to the library (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the track record was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>MusicBrainz recording identifier.</summary>
    public string? MusicBrainzRecordingId { get; set; }

    /// <summary>When the track was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; set; }

    /// <summary>Navigation to the album.</summary>
    public MusicAlbum? Album { get; set; }

    /// <summary>Artist associations for this track.</summary>
    public ICollection<TrackArtist> TrackArtists { get; set; } = new List<TrackArtist>();

    /// <summary>Genre associations for this track.</summary>
    public ICollection<TrackGenre> TrackGenres { get; set; } = new List<TrackGenre>();

    /// <summary>Playlist associations for this track.</summary>
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}
