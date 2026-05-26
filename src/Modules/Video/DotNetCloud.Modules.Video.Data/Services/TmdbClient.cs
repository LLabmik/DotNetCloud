using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Typed HTTP client implementation for TMDB API v3.
/// </summary>
public sealed class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly TmdbRateLimiter _rateLimiter;
    private readonly IVideoSettingsProvider _settingsProvider;
    private readonly ILogger<TmdbClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TmdbClient(HttpClient httpClient, TmdbRateLimiter rateLimiter, IVideoSettingsProvider settingsProvider, ILogger<TmdbClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TmdbMovieSearchResult>?> SearchMovieAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var query = Uri.EscapeDataString(title);
        var url = $"search/movie?api_key={apiKey}&query={query}&language=en-US";
        if (year.HasValue)
            url += $"&year={year.Value}";

        var response = await GetJsonAsync<TmdbSearchResponse<TmdbMovieSearchResult>>(url, cancellationToken);
        return response?.Results;
    }

    /// <inheritdoc />
    public async Task<TmdbMovieDetail?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var url = $"movie/{tmdbId}?api_key={apiKey}&language=en-US";
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

    // ─── TV Series Endpoints ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<TmdbTvSeriesSearchResult>?> SearchTvSeriesAsync(string query, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var encoded = Uri.EscapeDataString(query);
        var url = $"search/tv?api_key={apiKey}&query={encoded}&language=en-US";
        var response = await GetJsonAsync<TmdbSearchResponse<TmdbTvSeriesSearchResult>>(url, cancellationToken);
        return response?.Results;
    }

    /// <inheritdoc />
    public async Task<TmdbTvSeriesDetail?> GetTvSeriesAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var url = $"tv/{tmdbId}?api_key={apiKey}&language=en-US";
        return await GetJsonAsync<TmdbTvSeriesDetail>(url, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TmdbTvSeasonDetail?> GetTvSeasonAsync(int seriesTmdbId, int seasonNumber, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var url = $"tv/{seriesTmdbId}/season/{seasonNumber}?api_key={apiKey}&language=en-US";
        return await GetJsonAsync<TmdbTvSeasonDetail>(url, cancellationToken);
    }

    // ─── Collection (Franchise) Endpoints ────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<TmdbCollectionSearchResult>?> SearchCollectionAsync(string query, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var encoded = Uri.EscapeDataString(query);
        var url = $"search/collection?api_key={apiKey}&query={encoded}&language=en-US";
        var response = await GetJsonAsync<TmdbSearchResponse<TmdbCollectionSearchResult>>(url, cancellationToken);
        return response?.Results;
    }

    /// <inheritdoc />
    public async Task<TmdbCollectionDetail?> GetCollectionAsync(int collectionId, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync(cancellationToken);
        if (apiKey is null)
            return null;

        var url = $"collection/{collectionId}?api_key={apiKey}&language=en-US";
        return await GetJsonAsync<TmdbCollectionDetail>(url, cancellationToken);
    }

    /// <summary>
    /// Gets the TMDB API key from the settings provider (DB with file fallback).
    /// Returns null if not configured.
    /// </summary>
    private async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var key = await _settingsProvider.GetTmdbApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogInformation("TMDB API key is not configured. TMDB enrichment is disabled.");
            return null;
        }
        return key;
    }

    /// <summary>
    /// Generic internal model for TMDB search response wrapper.
    /// </summary>
    private sealed class TmdbSearchResponse<T>
    {
        [JsonPropertyName("results")]
        public List<T> Results { get; set; } = [];
    }
}
