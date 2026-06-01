namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Low-level client for TheAudioDB API v1.
/// Provides artist artwork URLs including logos, banners, and fanart.
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
