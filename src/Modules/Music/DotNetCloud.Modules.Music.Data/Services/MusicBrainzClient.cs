using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// HTTP client for the MusicBrainz Web Service v2 API.
/// Base URL: https://musicbrainz.org/ws/2/
/// Requires a descriptive User-Agent header.
/// </summary>
public sealed class MusicBrainzClient : IMusicBrainzClient
{
    private readonly HttpClient _httpClient;
    private readonly MusicBrainzRateLimiter _rateLimiter;
    private readonly ILogger<MusicBrainzClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzClient"/> class.
    /// </summary>
    public MusicBrainzClient(HttpClient httpClient, MusicBrainzRateLimiter rateLimiter, ILogger<MusicBrainzClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MusicBrainzArtistResult>?> SearchArtistAsync(string name, CancellationToken cancellationToken = default)
    {
        var encodedName = Uri.EscapeDataString(name);
        var url = $"artist/?query=artist:\"{encodedName}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbArtistSearchResponse>(json, JsonOptions);
            if (response?.Artists is null) return [];

            return response.Artists.Select(a => new MusicBrainzArtistResult
            {
                Id = a.Id,
                Name = a.Name,
                Score = a.Score,
                Disambiguation = a.Disambiguation
            }).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz artist search response");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MusicBrainzArtistDetail?> GetArtistAsync(string mbid, CancellationToken cancellationToken = default)
    {
        var url = $"artist/{Uri.EscapeDataString(mbid)}?inc=url-rels+annotation&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbArtistDetail>(json, JsonOptions);
            if (response is null) return null;

            string? wikipediaUrl = null;
            string? discogsUrl = null;
            string? officialUrl = null;

            if (response.Relations is not null)
            {
                foreach (var rel in response.Relations)
                {
                    var targetUrl = rel.Url?.Resource;
                    if (targetUrl is null) continue;

                    if (rel.Type == "wikipedia")
                        wikipediaUrl = targetUrl;
                    else if (rel.Type == "discogs")
                        discogsUrl = targetUrl;
                    else if (rel.Type == "official homepage")
                        officialUrl = targetUrl;
                }
            }

            return new MusicBrainzArtistDetail
            {
                Id = response.Id,
                Name = response.Name,
                Annotation = response.Annotation,
                WikipediaUrl = wikipediaUrl,
                DiscogsUrl = discogsUrl,
                OfficialUrl = officialUrl
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz artist detail response for {Mbid}", mbid);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MusicBrainzReleaseGroupResult>?> SearchReleaseGroupAsync(string album, string artist, CancellationToken cancellationToken = default)
    {
        var encodedAlbum = Uri.EscapeDataString(album);
        var encodedArtist = Uri.EscapeDataString(artist);
        var url = $"release-group/?query=releasegroup:\"{encodedAlbum}\" AND artist:\"{encodedArtist}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbReleaseGroupSearchResponse>(json, JsonOptions);
            if (response?.ReleaseGroups is null) return [];

            return response.ReleaseGroups.Select(rg => new MusicBrainzReleaseGroupResult
            {
                Id = rg.Id,
                Title = rg.Title,
                Score = rg.Score,
                PrimaryType = rg.PrimaryType
            }).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz release group search response");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MusicBrainzReleaseGroupDetail?> GetReleaseGroupAsync(string mbid, CancellationToken cancellationToken = default)
    {
        var url = $"release-group/{Uri.EscapeDataString(mbid)}?inc=releases&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbReleaseGroupDetail>(json, JsonOptions);
            if (response is null) return null;

            return new MusicBrainzReleaseGroupDetail
            {
                Id = response.Id,
                Title = response.Title,
                Releases = response.Releases?.Select(r => new MusicBrainzRelease
                {
                    Id = r.Id,
                    Title = r.Title,
                    Date = r.Date,
                    Country = r.Country
                }).ToList() ?? []
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz release group detail response for {Mbid}", mbid);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MusicBrainzRecordingResult>?> SearchRecordingAsync(string title, string artist, CancellationToken cancellationToken = default)
    {
        var encodedTitle = Uri.EscapeDataString(title);
        var encodedArtist = Uri.EscapeDataString(artist);
        var url = $"recording/?query=recording:\"{encodedTitle}\" AND artist:\"{encodedArtist}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbRecordingSearchResponse>(json, JsonOptions);
            if (response?.Recordings is null) return [];

            return response.Recordings.Select(r => new MusicBrainzRecordingResult
            {
                Id = r.Id,
                Title = r.Title,
                Score = r.Score,
                Length = r.Length
            }).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz recording search response");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MusicBrainzRecordingDetail?> GetRecordingAsync(string mbid, CancellationToken cancellationToken = default)
    {
        var url = $"recording/{Uri.EscapeDataString(mbid)}?fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null) return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbRecordingDetail>(json, JsonOptions);
            if (response is null) return null;

            return new MusicBrainzRecordingDetail
            {
                Id = response.Id,
                Title = response.Title,
                Length = response.Length
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz recording detail response for {Mbid}", mbid);
            return null;
        }
    }

    /// <summary>
    /// Sends a rate-limited GET request and returns the response body as string.
    /// Returns null on any HTTP error, timeout, or network failure.
    /// </summary>
    private async Task<string?> GetJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("MusicBrainz returned {StatusCode} for {Url}", response.StatusCode, relativeUrl);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MusicBrainz returned {StatusCode} for {Url}", response.StatusCode, relativeUrl);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling MusicBrainz for {Url}", relativeUrl);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout calling MusicBrainz for {Url}", relativeUrl);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    // ── Internal JSON deserialization models ─────────────────────────

    // These map directly to MusicBrainz JSON response structures using
    // kebab-case-lower naming policy (e.g., "release-groups", "primary-type").

    private sealed class MbArtistSearchResponse
    {
        public List<MbArtistSearchItem>? Artists { get; set; }
    }

    private sealed class MbArtistSearchItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public string? Disambiguation { get; set; }
    }

    private sealed class MbArtistDetail
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Annotation { get; set; }
        public List<MbRelation>? Relations { get; set; }
    }

    private sealed class MbRelation
    {
        public string? Type { get; set; }
        public MbRelationUrl? Url { get; set; }
    }

    private sealed class MbRelationUrl
    {
        public string? Resource { get; set; }
    }

    private sealed class MbReleaseGroupSearchResponse
    {
        [JsonPropertyName("release-groups")]
        public List<MbReleaseGroupSearchItem>? ReleaseGroups { get; set; }
    }

    private sealed class MbReleaseGroupSearchItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Score { get; set; }
        [JsonPropertyName("primary-type")]
        public string? PrimaryType { get; set; }
    }

    private sealed class MbReleaseGroupDetail
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public List<MbRelease>? Releases { get; set; }
    }

    private sealed class MbRelease
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Date { get; set; }
        public string? Country { get; set; }
    }

    private sealed class MbRecordingSearchResponse
    {
        public List<MbRecordingSearchItem>? Recordings { get; set; }
    }

    private sealed class MbRecordingSearchItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Score { get; set; }
        public int? Length { get; set; }
    }

    private sealed class MbRecordingDetail
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int? Length { get; set; }
    }
}
