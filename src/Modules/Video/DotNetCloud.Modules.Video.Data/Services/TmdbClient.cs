using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Typed HTTP client implementation for TMDB API v3.
/// </summary>
public sealed class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly TmdbRateLimiter _rateLimiter;
    private readonly string? _apiKey;
    private readonly ILogger<TmdbClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TmdbClient(HttpClient httpClient, TmdbRateLimiter rateLimiter, IConfiguration configuration, ILogger<TmdbClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _apiKey = configuration["Video:Enrichment:TmdbApiKey"];
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_apiKey))
            _logger.LogInformation("TMDB API key is not configured (Video:Enrichment:TmdbApiKey). TMDB enrichment is disabled.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TmdbMovieSearchResult>?> SearchMovieAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
            return null;

        var query = Uri.EscapeDataString(title);
        var url = $"search/movie?api_key={_apiKey}&query={query}&language=en-US";
        if (year.HasValue)
            url += $"&year={year.Value}";

        var response = await GetJsonAsync<TmdbSearchResponse>(url, cancellationToken);
        return response?.Results;
    }

    /// <inheritdoc />
    public async Task<TmdbMovieDetail?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
            return null;

        var url = $"movie/{tmdbId}?api_key={_apiKey}&language=en-US";
        return await GetJsonAsync<TmdbMovieDetail>(url, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TmdbImageResult?> DownloadPosterAsync(string posterPath, string size = "w500", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
            return null;

        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var url = $"https://image.tmdb.org/t/p/{size}{posterPath}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB poster download returned {StatusCode} for {Path}", (int)response.StatusCode, posterPath);
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return new TmdbImageResult { Data = data, MimeType = mimeType };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error downloading TMDB poster for {Path}", posterPath);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<T?> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB API returned {StatusCode} for {Uri}", (int)response.StatusCode, requestUri);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error calling TMDB API for {Uri}", requestUri);
            return null;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Internal model for TMDB search response wrapper.
    /// </summary>
    private sealed class TmdbSearchResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbMovieSearchResult> Results { get; set; } = [];
    }
}
