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
}
