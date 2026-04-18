using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Music.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// HTTP client for the Cover Art Archive API (https://coverartarchive.org/).
/// Fetches album cover art for MusicBrainz releases.
/// </summary>
public sealed class CoverArtArchiveClient : ICoverArtArchiveClient
{
    private readonly HttpClient _httpClient;
    private readonly MusicBrainzRateLimiter _rateLimiter;
    private readonly ILogger<CoverArtArchiveClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverArtArchiveClient"/> class.
    /// </summary>
    public CoverArtArchiveClient(HttpClient httpClient, MusicBrainzRateLimiter rateLimiter, ILogger<CoverArtArchiveClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CoverArtResult?> GetFrontCoverAsync(string releaseMbid, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var url = $"release/{Uri.EscapeDataString(releaseMbid)}/front";
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                _logger.LogDebug("No front cover art for release {ReleaseMbid}", releaseMbid);
                return null;
            }

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Cover Art Archive returned {StatusCode} for release {ReleaseMbid}", response.StatusCode, releaseMbid);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cover Art Archive returned {StatusCode} for release {ReleaseMbid}", response.StatusCode, releaseMbid);
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (data.Length == 0)
            {
                _logger.LogDebug("Cover Art Archive returned empty body for release {ReleaseMbid}", releaseMbid);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var mimeType = contentType switch
            {
                "image/png" => "image/png",
                "image/jpeg" => "image/jpeg",
                "image/gif" => "image/gif",
                _ => "image/jpeg" // Default to JPEG for unknown types
            };

            return new CoverArtResult
            {
                Data = data,
                MimeType = mimeType,
                ReleaseMbid = releaseMbid
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error fetching cover art for release {ReleaseMbid}", releaseMbid);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout fetching cover art for release {ReleaseMbid}", releaseMbid);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CoverArtImage>?> GetCoverListAsync(string releaseMbid, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var url = $"release/{Uri.EscapeDataString(releaseMbid)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cover Art Archive returned {StatusCode} for cover list of release {ReleaseMbid}", response.StatusCode, releaseMbid);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<CaaCoverListResponse>(json, JsonOptions);

            return result?.Images?.Select(i => new CoverArtImage
            {
                Id = i.Id,
                Types = i.Types ?? [],
                Front = i.Front,
                Back = i.Back,
                Image = i.Image
            }).ToList() ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error fetching cover list for release {ReleaseMbid}", releaseMbid);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout fetching cover list for release {ReleaseMbid}", releaseMbid);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cover list for release {ReleaseMbid}", releaseMbid);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<CoverArtResult?> GetFrontCoverFromReleasesAsync(IReadOnlyList<MusicBrainzRelease> releases, CancellationToken cancellationToken = default)
    {
        if (releases.Count == 0)
        {
            return null;
        }

        foreach (var release in releases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await GetFrontCoverAsync(release.Id, cancellationToken);
            if (result is not null)
            {
                _logger.LogDebug("Found cover art from release {ReleaseMbid} ({Title})", release.Id, release.Title);
                return result;
            }
        }

        _logger.LogDebug("No cover art found in any of {Count} releases", releases.Count);
        return null;
    }

    // ── Internal JSON deserialization models ─────────────────────────

    private sealed class CaaCoverListResponse
    {
        public List<CaaCoverImage>? Images { get; set; }
    }

    private sealed class CaaCoverImage
    {
        public long Id { get; set; }
        public List<string>? Types { get; set; }
        public bool Front { get; set; }
        public bool Back { get; set; }
        public string? Image { get; set; }
    }
}
