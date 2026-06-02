using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// HTTP client for TheAudioDB API v1.
/// Base URL: https://www.theaudiodb.com/api/v1/json/{apiKey}/
/// Provides artist artwork including logos, banners, and fanart.
/// </summary>
public sealed class AudioDbClient : IAudioDbClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AudioDbClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioDbClient"/> class.
    /// </summary>
    public AudioDbClient(HttpClient httpClient, ILogger<AudioDbClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AudioDbAlbumResult>?> SearchAlbumAsync(string albumTitle, string artistName, CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedArtist = Uri.EscapeDataString(artistName);
            var encodedAlbum = Uri.EscapeDataString(albumTitle);
            var response = await _httpClient.GetFromJsonAsync<AudioDbAlbumSearchResponse>(
                $"searchalbum.php?s={encodedArtist}&a={encodedAlbum}",
                cancellationToken);

            var results = response?.Album;
            if (results is null || results.Count == 0)
            {
                _logger.LogDebug("No TheAudioDB album found for '{AlbumTitle}' by '{ArtistName}'", albumTitle, artistName);
                return [];
            }

            return results.Select(a => new AudioDbAlbumResult
            {
                AlbumId = a.IdAlbum ?? string.Empty,
                AlbumTitle = a.StrAlbum ?? albumTitle,
                ArtistName = a.StrArtist ?? artistName,
                ThumbnailUrl = string.IsNullOrEmpty(a.StrAlbumThumb) ? null : a.StrAlbumThumb,
                Year = int.TryParse(a.IntYearReleased, out var y) ? y : null,
                Description = string.IsNullOrEmpty(a.StrDescriptionEN) ? null : a.StrDescriptionEN
            }).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "TheAudioDB album search failed for '{AlbumTitle}' by '{ArtistName}'", albumTitle, artistName);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize TheAudioDB album search response for '{AlbumTitle}' by '{ArtistName}'", albumTitle, artistName);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<AudioDbArtistArtwork?> GetArtistArtworkAsync(string musicBrainzId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<AudioDbArtistResponse>(
                $"artist-mb.php?i={Uri.EscapeDataString(musicBrainzId)}",
                cancellationToken);

            var artist = response?.Artists?.FirstOrDefault();
            if (artist is null)
            {
                _logger.LogDebug("No TheAudioDB artist found for MusicBrainz ID {Mbid}", musicBrainzId);
                return null;
            }

            return new AudioDbArtistArtwork
            {
                LogoUrl = string.IsNullOrEmpty(artist.StrArtistLogo) ? null : artist.StrArtistLogo,
                ThumbUrl = string.IsNullOrEmpty(artist.StrArtistThumb) ? null : artist.StrArtistThumb,
                BannerUrl = string.IsNullOrEmpty(artist.StrArtistBanner) ? null : artist.StrArtistBanner,
                FanartUrl = string.IsNullOrEmpty(artist.StrArtistFanart) ? null : artist.StrArtistFanart
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "TheAudioDB request failed for MusicBrainz ID {Mbid}", musicBrainzId);
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize TheAudioDB response for MusicBrainz ID {Mbid}", musicBrainzId);
            return null;
        }
    }

    // ── JSON response DTOs ────────────────────────────────────────────

    private sealed record AudioDbArtistResponse
    {
        [JsonPropertyName("artists")]
        public List<AudioDbArtistEntry>? Artists { get; init; }
    }

    private sealed record AudioDbArtistEntry
    {
        [JsonPropertyName("strArtistLogo")]
        public string? StrArtistLogo { get; init; }

        [JsonPropertyName("strArtistThumb")]
        public string? StrArtistThumb { get; init; }

        [JsonPropertyName("strArtistBanner")]
        public string? StrArtistBanner { get; init; }

        [JsonPropertyName("strArtistFanart")]
        public string? StrArtistFanart { get; init; }
    }

    // ── Album search JSON response DTOs ─────────────────────────

    private sealed record AudioDbAlbumSearchResponse
    {
        [JsonPropertyName("album")]
        public List<AudioDbAlbumEntry>? Album { get; init; }
    }

    private sealed record AudioDbAlbumEntry
    {
        [JsonPropertyName("idAlbum")]
        public string? IdAlbum { get; init; }

        [JsonPropertyName("strAlbum")]
        public string? StrAlbum { get; init; }

        [JsonPropertyName("strArtist")]
        public string? StrArtist { get; init; }

        [JsonPropertyName("strAlbumThumb")]
        public string? StrAlbumThumb { get; init; }

        [JsonPropertyName("strDescriptionEN")]
        public string? StrDescriptionEN { get; init; }

        [JsonPropertyName("intYearReleased")]
        public string? IntYearReleased { get; init; }
    }
}
