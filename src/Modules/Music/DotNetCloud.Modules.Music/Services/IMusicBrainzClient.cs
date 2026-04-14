namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Low-level client for the MusicBrainz Web Service v2 API.
/// </summary>
public interface IMusicBrainzClient
{
    /// <summary>
    /// Searches for artists by name.
    /// </summary>
    /// <param name="name">Artist name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching artists, or null if the request failed.</returns>
    Task<IReadOnlyList<MusicBrainzArtistResult>?> SearchArtistAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full artist details including URL relations and annotation.
    /// </summary>
    /// <param name="mbid">MusicBrainz artist ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Artist details, or null if not found or request failed.</returns>
    Task<MusicBrainzArtistDetail?> GetArtistAsync(string mbid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for release groups (albums) by title and artist name.
    /// </summary>
    /// <param name="album">Album title.</param>
    /// <param name="artist">Artist name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching release groups, or null if the request failed.</returns>
    Task<IReadOnlyList<MusicBrainzReleaseGroupResult>?> SearchReleaseGroupAsync(string album, string artist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full release group details including releases.
    /// </summary>
    /// <param name="mbid">MusicBrainz release group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Release group details, or null if not found or request failed.</returns>
    Task<MusicBrainzReleaseGroupDetail?> GetReleaseGroupAsync(string mbid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for recordings by title and artist name.
    /// </summary>
    /// <param name="title">Track title.</param>
    /// <param name="artist">Artist name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching recordings, or null if the request failed.</returns>
    Task<IReadOnlyList<MusicBrainzRecordingResult>?> SearchRecordingAsync(string title, string artist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full recording details.
    /// </summary>
    /// <param name="mbid">MusicBrainz recording ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recording details, or null if not found or request failed.</returns>
    Task<MusicBrainzRecordingDetail?> GetRecordingAsync(string mbid, CancellationToken cancellationToken = default);
}

// ── MusicBrainz response DTOs ───────────────────────────────────────

/// <summary>
/// A single artist result from a MusicBrainz search.
/// </summary>
public sealed record MusicBrainzArtistResult
{
    /// <summary>MusicBrainz artist ID.</summary>
    public required string Id { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Search relevance score (0-100).</summary>
    public int Score { get; init; }

    /// <summary>Disambiguation string to distinguish similarly-named artists.</summary>
    public string? Disambiguation { get; init; }
}

/// <summary>
/// Full artist detail from MusicBrainz, including URL relations and annotation.
/// </summary>
public sealed record MusicBrainzArtistDetail
{
    /// <summary>MusicBrainz artist ID.</summary>
    public required string Id { get; init; }

    /// <summary>Artist name.</summary>
    public required string Name { get; init; }

    /// <summary>Artist biography from MusicBrainz annotation.</summary>
    public string? Annotation { get; init; }

    /// <summary>Wikipedia URL extracted from URL relations.</summary>
    public string? WikipediaUrl { get; init; }

    /// <summary>Discogs URL extracted from URL relations.</summary>
    public string? DiscogsUrl { get; init; }

    /// <summary>Official website URL extracted from URL relations.</summary>
    public string? OfficialUrl { get; init; }
}

/// <summary>
/// A single release group (album) result from a MusicBrainz search.
/// </summary>
public sealed record MusicBrainzReleaseGroupResult
{
    /// <summary>MusicBrainz release group ID.</summary>
    public required string Id { get; init; }

    /// <summary>Release group title.</summary>
    public required string Title { get; init; }

    /// <summary>Search relevance score (0-100).</summary>
    public int Score { get; init; }

    /// <summary>Primary type (e.g. "Album", "Single", "EP").</summary>
    public string? PrimaryType { get; init; }
}

/// <summary>
/// Full release group detail from MusicBrainz, including associated releases.
/// </summary>
public sealed record MusicBrainzReleaseGroupDetail
{
    /// <summary>MusicBrainz release group ID.</summary>
    public required string Id { get; init; }

    /// <summary>Release group title.</summary>
    public required string Title { get; init; }

    /// <summary>List of releases in this release group.</summary>
    public IReadOnlyList<MusicBrainzRelease> Releases { get; init; } = [];
}

/// <summary>
/// A specific release within a release group.
/// </summary>
public sealed record MusicBrainzRelease
{
    /// <summary>MusicBrainz release ID.</summary>
    public required string Id { get; init; }

    /// <summary>Release title.</summary>
    public required string Title { get; init; }

    /// <summary>Release date (may be partial, e.g. "1973" or "1973-03-01").</summary>
    public string? Date { get; init; }

    /// <summary>Release country code (e.g. "US", "GB").</summary>
    public string? Country { get; init; }
}

/// <summary>
/// A single recording result from a MusicBrainz search.
/// </summary>
public sealed record MusicBrainzRecordingResult
{
    /// <summary>MusicBrainz recording ID.</summary>
    public required string Id { get; init; }

    /// <summary>Recording title.</summary>
    public required string Title { get; init; }

    /// <summary>Search relevance score (0-100).</summary>
    public int Score { get; init; }

    /// <summary>Recording duration in milliseconds.</summary>
    public int? Length { get; init; }
}

/// <summary>
/// Full recording detail from MusicBrainz.
/// </summary>
public sealed record MusicBrainzRecordingDetail
{
    /// <summary>MusicBrainz recording ID.</summary>
    public required string Id { get; init; }

    /// <summary>Recording title.</summary>
    public required string Title { get; init; }

    /// <summary>Recording duration in milliseconds.</summary>
    public int? Length { get; init; }
}
