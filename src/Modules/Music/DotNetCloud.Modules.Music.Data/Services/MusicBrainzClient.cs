using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    /// <summary>Regex for parenthetical suffixes: (Remastered), (Deluxe Edition), (1994 Remaster), etc.</summary>
    private static readonly Regex ParentheticalSuffix = new(
        @"\s*\([^)]*\)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Regex for truncated/broken parentheticals from ID3v1 tag truncation.
    /// Matches a trailing open-paren with partial content but no closing paren,
    /// e.g. "(1994 ", "(1994 Remaste", "(Dis", "(Deluxe Ed".
    /// Only strips if the open-paren appears near the end (last 15 chars),
    /// indicating truncation rather than a legitimate title starting with '('.
    /// </summary>
    private static readonly Regex TruncatedParenthetical = new(
        @"\s*\([^)]{1,25}$", RegexOptions.Compiled);

    /// <summary>
    /// Regex for tagger-added Roman numeral I suffix. The debut Led Zeppelin
    /// album is just "Led Zeppelin" on MB, not "Led Zeppelin I". Strips only
    /// solitary " I" at end; "II", "III", "IV" are kept (legitimate titles).
    /// </summary>
    private static readonly Regex TaggerRomanNumeralI = new(
        @"\s+I$", RegexOptions.Compiled);

    /// <summary>
    /// Regex for trailing volume numbers: "Vol. 1", "Vol 2", "Volume One", etc.
    /// These break MB lookups for compilations like "Early Days: The Best of
    /// Led Zeppelin, Volume One" (tagged as "Early Days Vol. 1").
    /// </summary>
    private static readonly Regex TrailingVolumeNumber = new(
        @"\s+Vol\.?\s*\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Characters that break MusicBrainz Lucene query syntax when unescaped.
    /// </summary>
    private static readonly Regex LuceneSpecialChars = new(
        @"([+\-!(){}\[\]^""~*?:\\/])", RegexOptions.Compiled);

    /// <summary>
    /// Strips trailing parenthetical suffixes (including truncated ID3v1 variants)
    /// and escapes Lucene special characters for safe MusicBrainz queries.
    /// </summary>
    private static string SanitizeMusicBrainzQuery(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        // Strip complete parenthetical suffix — these break Lucene grouping syntax
        var cleaned = ParentheticalSuffix.Replace(value, "").Trim();

        // Strip truncated/broken parenthetical from ID3v1 tag truncation
        // (e.g. "Led Zeppelin III (1994 Remaste" → "Led Zeppelin III")
        cleaned = TruncatedParenthetical.Replace(cleaned, "").Trim();

        // Only matches solitary "I" — "II", "III", "IV" are legitimate album titles
        cleaned = TaggerRomanNumeralI.Replace(cleaned, "").Trim();

        // Strip trailing volume numbers (e.g. "Vol. 1", "Vol 2")
        // These break MB lookup for compilations tagged with volume suffixes
        cleaned = TrailingVolumeNumber.Replace(cleaned, "").Trim();
        // Escape Lucene special characters LAST
        cleaned = LuceneSpecialChars.Replace(cleaned, @"\$1");

        // Escape Lucene special characters LAST — stripping must happen first so
        // hyphens and periods in volume numbers aren't escaped before regex matching.
        // Strip tagger-added Roman numeral I suffix (e.g. "Led Zeppelin I" → "Led Zeppelin")
        return cleaned;
    }

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
        var safeName = SanitizeMusicBrainzQuery(name);
        var encodedName = Uri.EscapeDataString(safeName);
        var url = $"artist/?query=artist:\"{encodedName}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbArtistSearchResponse>(json, JsonOptions);
            if (response?.Artists is null)
                return [];

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
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbArtistDetail>(json, JsonOptions);
            if (response is null)
                return null;

            string? wikipediaUrl = null;
            string? discogsUrl = null;
            string? officialUrl = null;

            if (response.Relations is not null)
            {
                foreach (var rel in response.Relations)
                {
                    var targetUrl = rel.Url?.Resource;
                    if (targetUrl is null)
                        continue;

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
        // Search the 'release' endpoint instead of 'release-group' — the release index
        // handles spelling variations (e.g. favorite/favourite) much better, and each
        // release result includes its parent release-group ID.
        var safeAlbum = SanitizeMusicBrainzQuery(album);
        var safeArtist = SanitizeMusicBrainzQuery(artist);
        var encodedAlbum = Uri.EscapeDataString(safeAlbum);
        var encodedArtist = Uri.EscapeDataString(safeArtist);
        var url = $"release/?query=release:\"{encodedAlbum}\" AND artist:\"{encodedArtist}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbReleaseSearchResponse>(json, JsonOptions);
            if (response?.Releases is null)
                return [];

            // Extract release-group info from each release, deduplicate by ID,
            // and take the highest score for each unique release-group.
            var seen = new Dictionary<string, MusicBrainzReleaseGroupResult>();
            foreach (var release in response.Releases)
            {
                var rg = release.ReleaseGroup;
                if (rg is null)
                    continue;

                if (!seen.TryGetValue(rg.Id, out var existing) || release.Score > existing.Score)
                {
                    seen[rg.Id] = new MusicBrainzReleaseGroupResult
                    {
                        Id = rg.Id,
                        Title = rg.Title,
                        Score = release.Score,
                        PrimaryType = rg.PrimaryType
                    };
                }
            }

            // Sort: prefer exact title matches over score — prevents mashups like
            // "Led Zeppelin x Led Zeppelin" (score 100) from outranking the actual
            // debut album "Led Zeppelin" (score 99).
            var results = seen.Values
                .OrderByDescending(r => string.Equals(r.Title, safeAlbum, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(r => r.Score)
                .ToList();

            // Fallback: if phrase match returns nothing, retry with just the first
            // 2-3 words. Helps for compilations like "Early Days - The Best Of Led
            // Zeppelin Vol. 1" where MB uses colons and different wording.
            if (results.Count == 0)
            {
                var words = safeAlbum.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 3)
                {
                    var shortQuery = string.Join(' ', words.Take(3));
                    var encodedShort = Uri.EscapeDataString(shortQuery);
                    var fallbackUrl = "release/?query=release:%22" + encodedShort + "%22+AND+artist:%22" + encodedArtist + "%22&fmt=json";
                    var fallbackJson = await GetJsonAsync(fallbackUrl, cancellationToken);
                    if (fallbackJson is not null)
                    {
                        var fallbackResponse = JsonSerializer.Deserialize<MbReleaseSearchResponse>(fallbackJson, JsonOptions);
                        if (fallbackResponse?.Releases is not null)
                        {
                            var fallbackSeen = new Dictionary<string, MusicBrainzReleaseGroupResult>();
                            foreach (var release in fallbackResponse.Releases)
                            {
                                var rg = release.ReleaseGroup;
                                if (rg is null) continue;
                                if (!fallbackSeen.TryGetValue(rg.Id, out var fexisting) || release.Score > fexisting.Score)
                                    fallbackSeen[rg.Id] = new MusicBrainzReleaseGroupResult { Id = rg.Id, Title = rg.Title, Score = release.Score, PrimaryType = rg.PrimaryType };
                            }
                            results = fallbackSeen.Values
                                .OrderByDescending(r => string.Equals(r.Title, shortQuery, StringComparison.OrdinalIgnoreCase))
                                .ThenByDescending(r => r.Score)
                                .ToList();
                        }
                    }
                }
            }

            return results;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz release search response");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MusicBrainzReleaseGroupDetail?> GetReleaseGroupAsync(string mbid, CancellationToken cancellationToken = default)
    {
        var url = $"release-group/{Uri.EscapeDataString(mbid)}?inc=releases&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbReleaseGroupDetail>(json, JsonOptions);
            if (response is null)
                return null;

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
        var safeTitle = SanitizeMusicBrainzQuery(title);
        var safeArtist = SanitizeMusicBrainzQuery(artist);
        var encodedTitle = Uri.EscapeDataString(safeTitle);
        var encodedArtist = Uri.EscapeDataString(safeArtist);
        var url = $"recording/?query=recording:\"{encodedTitle}\" AND artist:\"{encodedArtist}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbRecordingSearchResponse>(json, JsonOptions);
            if (response?.Recordings is null)
                return [];

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
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbRecordingDetail>(json, JsonOptions);
            if (response is null)
                return null;

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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MusicBrainzReleaseGroupResult>?> SearchReleaseGroupByArtistMbidAsync(string artistMbid, string albumTitle, CancellationToken cancellationToken = default)
    {
        // Search the 'release' endpoint for better spelling variation handling.
        var safeTitle = SanitizeMusicBrainzQuery(albumTitle);
        var encodedTitle = Uri.EscapeDataString(safeTitle);
        var url = $"release/?query=arid:{Uri.EscapeDataString(artistMbid)} AND release:\"{encodedTitle}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbReleaseSearchResponse>(json, JsonOptions);
            if (response?.Releases is null)
                return [];

            var seen = new Dictionary<string, MusicBrainzReleaseGroupResult>();
            foreach (var release in response.Releases)
            {
                var rg = release.ReleaseGroup;
                if (rg is null)
                    continue;

                if (!seen.TryGetValue(rg.Id, out var existing) || release.Score > existing.Score)
                {
                    seen[rg.Id] = new MusicBrainzReleaseGroupResult
                    {
                        Id = rg.Id,
                        Title = rg.Title,
                        Score = release.Score,
                        PrimaryType = rg.PrimaryType
                    };
                }
            }

            // Sort: prefer exact title matches over score — prevents mashups like
            // "Led Zeppelin x Led Zeppelin" (score 100) from outranking the actual
            // debut album "Led Zeppelin" (score 99).
            return seen.Values
                .OrderByDescending(r => string.Equals(r.Title, safeTitle, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(r => r.Score)
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz release search by artist MBID response");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MusicBrainzRecordingResult>?> SearchRecordingByArtistMbidAsync(string artistMbid, string trackTitle, CancellationToken cancellationToken = default)
    {
        var encodedTitle = Uri.EscapeDataString(trackTitle);
        var url = $"recording/?query=arid:{Uri.EscapeDataString(artistMbid)} AND recording:\"{encodedTitle}\"&fmt=json";

        var json = await GetJsonAsync(url, cancellationToken);
        if (json is null)
            return null;

        try
        {
            var response = JsonSerializer.Deserialize<MbRecordingSearchResponse>(json, JsonOptions);
            if (response?.Recordings is null)
                return [];

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
            _logger.LogWarning(ex, "Failed to deserialize MusicBrainz recording search by artist MBID response");
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

    private sealed class MbReleaseSearchResponse
    {
        public List<MbReleaseSearchItem>? Releases { get; set; }
    }

    private sealed class MbReleaseSearchItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Score { get; set; }
        [JsonPropertyName("release-group")]
        public MbReleaseGroupRef? ReleaseGroup { get; set; }
    }

    private sealed class MbReleaseGroupRef
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        [JsonPropertyName("primary-type")]
        public string? PrimaryType { get; set; }
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
