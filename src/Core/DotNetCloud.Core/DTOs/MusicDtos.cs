namespace DotNetCloud.Core.DTOs;

// ── Artist DTOs ─────────────────────────────────────────────────────

/// <summary>
/// Represents a music artist.
/// </summary>
public sealed record ArtistDto
{
    /// <summary>Unique identifier for this artist.</summary>
    public required Guid Id { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional sort name (e.g. "Beatles, The").</summary>
    public string? SortName { get; init; }

    /// <summary>Number of albums by this artist.</summary>
    public int AlbumCount { get; init; }

    /// <summary>Number of tracks by this artist.</summary>
    public int TrackCount { get; init; }

    /// <summary>Whether this artist is starred by the current user.</summary>
    public bool IsStarred { get; init; }

    /// <summary>When the artist record was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Artist biography and external links from MusicBrainz enrichment.
/// </summary>
public sealed record ArtistBioDto
{
    /// <summary>Artist identifier.</summary>
    public required Guid ArtistId { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Biography text from MusicBrainz annotation.</summary>
    public string? Biography { get; init; }

    /// <summary>Artist image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Wikipedia page URL.</summary>
    public string? WikipediaUrl { get; init; }

    /// <summary>Discogs page URL.</summary>
    public string? DiscogsUrl { get; init; }

    /// <summary>Official website URL.</summary>
    public string? OfficialUrl { get; init; }

    /// <summary>MusicBrainz artist identifier.</summary>
    public string? MusicBrainzId { get; init; }

    /// <summary>When the artist was last enriched from external sources (UTC).</summary>
    public DateTime? LastEnrichedAt { get; init; }
}

// ── Album DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a music album.
/// </summary>
public sealed record MusicAlbumDto
{
    /// <summary>Unique identifier for this album.</summary>
    public required Guid Id { get; init; }

    /// <summary>Album title.</summary>
    public required string Title { get; init; }

    /// <summary>Primary artist ID.</summary>
    public required Guid ArtistId { get; init; }

    /// <summary>Primary artist name.</summary>
    public required string ArtistName { get; init; }

    /// <summary>Release year.</summary>
    public int? Year { get; init; }

    /// <summary>Primary genre name.</summary>
    public string? Genre { get; init; }

    /// <summary>Number of tracks on this album.</summary>
    public int TrackCount { get; init; }

    /// <summary>Total duration of all tracks.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>Whether album art is available.</summary>
    public bool HasCoverArt { get; init; }

    /// <summary>Whether this album is starred by the current user.</summary>
    public bool IsStarred { get; init; }

    /// <summary>When the album was added to the library (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

// ── Track DTOs ──────────────────────────────────────────────────────

/// <summary>
/// Represents a music track.
/// </summary>
public sealed record TrackDto
{
    /// <summary>Unique identifier for this track.</summary>
    public required Guid Id { get; init; }

    /// <summary>The FileNode ID that this track references.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>Track title.</summary>
    public required string Title { get; init; }

    /// <summary>Track number on the album.</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Disc number.</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Track duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Audio bitrate in bps.</summary>
    public long? Bitrate { get; init; }

    /// <summary>MIME type (e.g. "audio/flac").</summary>
    public required string MimeType { get; init; }

    /// <summary>Album ID this track belongs to.</summary>
    public Guid? AlbumId { get; init; }

    /// <summary>Album title.</summary>
    public string? AlbumTitle { get; init; }

    /// <summary>Primary artist ID.</summary>
    public required Guid ArtistId { get; init; }

    /// <summary>Primary artist name.</summary>
    public required string ArtistName { get; init; }

    /// <summary>Genre name.</summary>
    public string? Genre { get; init; }

    /// <summary>Release year.</summary>
    public int? Year { get; init; }

    /// <summary>Whether this track is starred by the current user.</summary>
    public bool IsStarred { get; init; }

    /// <summary>When the track was added to the library (UTC).</summary>
    public required DateTime CreatedAt { get; init; }
}

// ── Playlist DTOs ───────────────────────────────────────────────────

/// <summary>
/// Represents a playlist.
/// </summary>
public sealed record PlaylistDto
{
    /// <summary>Unique identifier for this playlist.</summary>
    public required Guid Id { get; init; }

    /// <summary>The user who owns this playlist.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Playlist name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional playlist description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this playlist is public (visible to all users).</summary>
    public bool IsPublic { get; init; }

    /// <summary>Number of tracks in this playlist.</summary>
    public int TrackCount { get; init; }

    /// <summary>Total duration of all tracks.</summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>When the playlist was created (UTC).</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the playlist was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request to create a new playlist.
/// </summary>
public sealed record CreatePlaylistDto
{
    /// <summary>Playlist name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the playlist is public.</summary>
    public bool IsPublic { get; init; }
}

/// <summary>
/// Request to update a playlist.
/// </summary>
public sealed record UpdatePlaylistDto
{
    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated description.</summary>
    public string? Description { get; init; }

    /// <summary>Updated visibility.</summary>
    public bool? IsPublic { get; init; }
}

// ── Player DTOs ─────────────────────────────────────────────────────

/// <summary>
/// Represents the current playing state.
/// </summary>
public sealed record NowPlayingDto
{
    /// <summary>The currently playing track.</summary>
    public required TrackDto Track { get; init; }

    /// <summary>Current playback position.</summary>
    public TimeSpan Position { get; init; }

    /// <summary>The user who is playing.</summary>
    public required Guid UserId { get; init; }

    /// <summary>When playback started (UTC).</summary>
    public required DateTime StartedAt { get; init; }
}

// ── Equalizer DTOs ──────────────────────────────────────────────────

/// <summary>
/// Represents an equalizer preset.
/// </summary>
public sealed record EqPresetDto
{
    /// <summary>Unique identifier for this preset.</summary>
    public required Guid Id { get; init; }

    /// <summary>Preset name (e.g. "Rock", "Jazz", "Flat").</summary>
    public required string Name { get; init; }

    /// <summary>Whether this is a built-in preset (non-deletable).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>Band gains: keys are frequency labels (e.g. "60Hz"), values are gain in dB.</summary>
    public required IReadOnlyDictionary<string, double> Bands { get; init; }
}

/// <summary>
/// Request to create or update an EQ preset.
/// </summary>
public sealed record SaveEqPresetDto
{
    /// <summary>Preset name.</summary>
    public required string Name { get; init; }

    /// <summary>Band gains: keys are frequency labels, values are gain in dB.</summary>
    public required IDictionary<string, double> Bands { get; init; }
}

// ── Enrichment DTOs ─────────────────────────────────────────────────

/// <summary>
/// Progress information for metadata enrichment operations.
/// </summary>
public sealed record EnrichmentProgress
{
    /// <summary>Current enrichment phase (e.g. "Enriching artists...", "Enriching albums...", "Fetching cover art...").</summary>
    public required string Phase { get; init; }

    /// <summary>Number of items processed so far in the current phase.</summary>
    public int Current { get; init; }

    /// <summary>Total items to process in the current phase.</summary>
    public int Total { get; init; }

    /// <summary>Name of the item currently being processed.</summary>
    public string? CurrentItem { get; init; }

    /// <summary>Number of album covers found so far.</summary>
    public int AlbumArtFound { get; init; }

    /// <summary>Number of artist biographies found so far.</summary>
    public int ArtistBiosFound { get; init; }
}

// ── Library Scan DTOs ───────────────────────────────────────────────

/// <summary>
/// Real-time progress information for an in-progress library scan.
/// Reported via <see cref="IProgress{T}"/> during scan and enrichment phases.
/// </summary>
public sealed record LibraryScanProgress
{
    /// <summary>Current scan phase ("Discovering files", "Extracting metadata", "Enriching metadata", "Complete").</summary>
    public required string Phase { get; init; }

    /// <summary>Name of the file currently being processed.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Number of files processed so far.</summary>
    public int FilesProcessed { get; init; }

    /// <summary>Total files to process.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Tracks successfully added.</summary>
    public int TracksAdded { get; init; }

    /// <summary>Tracks updated (re-indexed).</summary>
    public int TracksUpdated { get; init; }

    /// <summary>Tracks skipped (already up to date).</summary>
    public int TracksSkipped { get; init; }

    /// <summary>Tracks that failed to index.</summary>
    public int TracksFailed { get; init; }

    /// <summary>Album covers fetched from external source.</summary>
    public int AlbumArtFetched { get; init; }

    /// <summary>Completion percentage (0-100).</summary>
    public int PercentComplete { get; init; }

    /// <summary>Time elapsed since scan started.</summary>
    public TimeSpan ElapsedTime { get; init; }
}

/// <summary>
/// Result of a library scan operation.
/// </summary>
public sealed record LibraryScanResultDto
{
    /// <summary>Number of new tracks added.</summary>
    public int TracksAdded { get; init; }

    /// <summary>Number of existing tracks updated.</summary>
    public int TracksUpdated { get; init; }

    /// <summary>Number of tracks removed (files no longer exist).</summary>
    public int TracksRemoved { get; init; }

    /// <summary>Total tracks in library after scan.</summary>
    public int TotalTracks { get; init; }

    /// <summary>Total artists in library after scan.</summary>
    public int TotalArtists { get; init; }

    /// <summary>Total albums in library after scan.</summary>
    public int TotalAlbums { get; init; }

    /// <summary>Scan duration.</summary>
    public TimeSpan Duration { get; init; }
}
