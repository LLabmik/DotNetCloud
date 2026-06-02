namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Low-level client for TheAudioDB API v1.
/// Provides artist artwork URLs including logos, banners, and fanart,
/// and album art search.
/// </summary>
public interface IAudioDbClient
{
    /// <summary>
    /// Looks up an artist by MusicBrainz ID and returns artwork URLs.
    /// </summary>
    /// <param name="musicBrainzId">The MusicBrainz ID of the artist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Artist artwork info, or null if not found or request failed.</returns>
    Task<AudioDbArtistArtwork?> GetArtistArtworkAsync(string musicBrainzId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for albums by title and artist name on TheAudioDB.
    /// Returns album details including cover art thumbnails.
    /// </summary>
    /// <param name="albumTitle">Album title to search for.</param>
    /// <param name="artistName">Artist name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching album results, or null if the request failed.</returns>
    Task<IReadOnlyList<AudioDbAlbumResult>?> SearchAlbumAsync(string albumTitle, string artistName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Artwork URLs for an artist from TheAudioDB.
/// </summary>
public sealed record AudioDbArtistArtwork
{
    /// <summary>Transparent band logo URL (PNG).</summary>
    public string? LogoUrl { get; init; }

    /// <summary>Artist thumbnail image URL.</summary>
    public string? ThumbUrl { get; init; }

    /// <summary>Wide banner image URL for headers.</summary>
    public string? BannerUrl { get; init; }

    /// <summary>Large fanart/background image URL.</summary>
    public string? FanartUrl { get; init; }
}

/// <summary>
/// An album result from TheAudioDB search, including cover art URL.
/// </summary>
public sealed record AudioDbAlbumResult
{
    /// <summary>TheAudioDB album ID.</summary>
    public required string AlbumId { get; init; }

    /// <summary>Album title.</summary>
    public required string AlbumTitle { get; init; }

    /// <summary>Artist name.</summary>
    public required string ArtistName { get; init; }

    /// <summary>Cover art thumbnail URL.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Release year.</summary>
    public int? Year { get; init; }

    /// <summary>Album description or style.</summary>
    public string? Description { get; init; }
}
