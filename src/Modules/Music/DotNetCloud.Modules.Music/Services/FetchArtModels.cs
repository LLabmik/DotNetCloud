namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Search parameters for finding album art from external sources.
/// Pre-filled from the database but editable by the user.
/// </summary>
public sealed record FetchArtSearchRequest
{
    /// <summary>Album title to search for.</summary>
    public required string AlbumTitle { get; init; }

    /// <summary>Artist name to search for.</summary>
    public required string ArtistName { get; init; }

    /// <summary>Release year, if known.</summary>
    public int? Year { get; init; }

    /// <summary>MusicBrainz artist ID, if known, for precise matching.</summary>
    public string? ArtistMbid { get; init; }

    /// <summary>Sanitized album title as sent to MusicBrainz (read-only, for display).</summary>
    public string? SanitizedAlbumTitle { get; init; }

    /// <summary>Sanitized artist name as sent to MusicBrainz (read-only, for display).</summary>
    public string? SanitizedArtistName { get; init; }
}

/// <summary>
/// A single result from searching for album art across multiple sources.
/// </summary>
public sealed record FetchArtSearchResult
{
    /// <summary>Source identifier, e.g. "MusicBrainz" or "TheAudioDB".</summary>
    public required string Source { get; init; }

    /// <summary>Source-specific ID (MB release group ID or AudioDB album ID).</summary>
    public required string SourceId { get; init; }

    /// <summary>Album title from the source.</summary>
    public required string Title { get; init; }

    /// <summary>Artist name from the source.</summary>
    public string? ArtistName { get; init; }

    /// <summary>Primary type (e.g. "Album", "Single", "EP").</summary>
    public string? PrimaryType { get; init; }

    /// <summary>Search relevance score (0-100).</summary>
    public int Score { get; init; }

    /// <summary>URL to a thumbnail of the cover art, if available.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Release year from the source.</summary>
    public int? Year { get; init; }
}

/// <summary>
/// Request to apply a specific art candidate to an album.
/// </summary>
public sealed record FetchArtApplyRequest
{
    /// <summary>Source identifier that provided the selected result.</summary>
    public required string Source { get; init; }

    /// <summary>Source-specific ID (MB release group ID or AudioDB album ID).</summary>
    public required string SourceId { get; init; }

    /// <summary>Thumbnail URL from the search result, used to avoid re-fetching.</summary>
    public string? ThumbnailUrl { get; init; }
}

/// <summary>
/// Result of applying a selected art candidate to an album.
/// </summary>
public sealed record ApplyArtResult
{
    /// <summary>Whether the art was successfully applied.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result returned by the FetchArtModal when it closes.
/// </summary>
public sealed record FetchArtModalResult
{
    /// <summary>Whether cover art was successfully applied.</summary>
    public bool Success { get; init; }
}
