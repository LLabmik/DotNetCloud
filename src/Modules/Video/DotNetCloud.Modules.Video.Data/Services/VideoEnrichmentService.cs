using System.Text.RegularExpressions;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Orchestrates TMDB movie metadata and poster art enrichment for videos.
/// Writes enrichment data to <see cref="CanonicalTmdbData"/>.
/// </summary>
public sealed partial class VideoEnrichmentService : IVideoEnrichmentService
{
    private readonly VideoDbContext _db;
    private readonly ITmdbClient _tmdbClient;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly ILogger<VideoEnrichmentService> _logger;

    public VideoEnrichmentService(VideoDbContext db, ITmdbClient tmdbClient, IConfiguration configuration, ILogger<VideoEnrichmentService> logger)
    {
        _db = db;
        _tmdbClient = tmdbClient;
        _logger = logger;

        // Check file config as a startup fallback — runtime DB check happens in enrichment methods
        IsTmdbAvailable = !string.IsNullOrWhiteSpace(configuration["Video:Enrichment:TmdbApiKey"]);

        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _contentStorage = new ContentAddressedStorage(storageRoot);
    }

    /// <inheritdoc />
    public bool IsTmdbAvailable { get; }

    /// <inheritdoc />
    public async Task EnrichVideoAsync(Guid videoId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        // ── Load video and resolve canonical content hash ──
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        if (userVideo?.CanonicalVideo is null)
            return;

        var canonicalVideo = userVideo.CanonicalVideo;

        // Use embedded title (from ffprobe metadata) when available and non-empty,
        // otherwise fall back to the raw filename-derived title. Clean both for TMDB search.
        var rawTitle = !string.IsNullOrWhiteSpace(canonicalVideo.EmbeddedTitle)
            ? canonicalVideo.EmbeddedTitle
            : canonicalVideo.Title;
        var videoTitle = CleanSearchTitle(rawTitle);

        // Source year from embedded metadata first, then filename regex
        var year = canonicalVideo.EmbeddedDate is not null && TryExtractYearFromDate(canonicalVideo.EmbeddedDate, out var embeddedYear)
            ? embeddedYear
            : ExtractYear(canonicalVideo.FileName);

        // ── Priority-based TMDB lookup ──

        TmdbMovieDetail? detail = null;
        TmdbMovieSearchResult? searchResult = null;

        // Priority 1: Direct TMDB ID lookup if embedded
        if (canonicalVideo.EmbeddedTmdbId is not null)
        {
            detail = await _tmdbClient.GetMovieAsync(canonicalVideo.EmbeddedTmdbId.Value, cancellationToken);
            if (detail is not null)
            {
                _logger.LogDebug("Used direct TMDB ID lookup: {TmdbId}", canonicalVideo.EmbeddedTmdbId);
                // Create a search result from the detail for consistent downstream processing
                searchResult = new TmdbMovieSearchResult
                {
                    Id = detail.Id,
                    Title = detail.Title,
                    Overview = detail.Overview,
                    PosterPath = detail.PosterPath,
                    ReleaseDate = detail.ReleaseDate?.ToString("yyyy-MM-dd"),
                    VoteAverage = detail.VoteAverage
                };
            }
        }

        // Priority 2: IMDB ID lookup via TMDB cross-reference
        if (searchResult is null && canonicalVideo.EmbeddedImdbId is not null)
        {
            searchResult = await _tmdbClient.SearchMovieByImdbIdAsync(canonicalVideo.EmbeddedImdbId, cancellationToken);
            if (searchResult is not null)
            {
                _logger.LogDebug("Used IMDB ID cross-reference: {ImdbId}", canonicalVideo.EmbeddedImdbId);
                // Fetch full details since we only have search result fields
                detail = await _tmdbClient.GetMovieAsync(searchResult.Id, cancellationToken);
            }
        }

        // Priority 3: Search by title + year
        if (searchResult is null && !string.IsNullOrWhiteSpace(videoTitle))
        {
            searchResult = await TryMovieSearchAsync(videoTitle, year, videoId, cancellationToken);
            if (searchResult is not null)
                detail = await _tmdbClient.GetMovieAsync(searchResult.Id, cancellationToken);
        }

        // Priority 4: If year-filtered search returned 0, retry WITHOUT year
        // (the extracted year may be wrong — e.g. from a TV series air date or encoding noise)
        if (searchResult is null && !string.IsNullOrWhiteSpace(videoTitle) && year.HasValue)
        {
            _logger.LogDebug("Year-filtered search returned 0 for '{Title}'; retrying without year", videoTitle);
            searchResult = await TryMovieSearchAsync(videoTitle, null, videoId, cancellationToken);
            if (searchResult is not null)
                detail = await _tmdbClient.GetMovieAsync(searchResult.Id, cancellationToken);
        }

        // Priority 5: Try TV series search — either from series membership or as a fallback
        if (searchResult is null && !string.IsNullOrWhiteSpace(videoTitle))
        {
            // Check if this video belongs to a known series (movie franchise or TV series)
            var seriesName = await GetSeriesNameForVideoAsync(canonicalVideo.ContentHash, cancellationToken);
            var tvQuery = seriesName ?? videoTitle;

            (searchResult, detail) = await TryTvSeriesSearchWithDetailAsync(tvQuery, year, videoId, cancellationToken);

            // Retry TV series search without year
            if (searchResult is null && year.HasValue)
            {
                (searchResult, detail) = await TryTvSeriesSearchWithDetailAsync(tvQuery, null, videoId, cancellationToken);
            }
        }

        if (searchResult is null)
        {
            _logger.LogDebug("No TMDB results found for video {VideoId} ('{Title}')", videoId, videoTitle);
            return;
        }

        var best = searchResult;

        // ── Check/create CanonicalTmdbData ──
        var canonicalTmdb = await _db.CanonicalTmdbData
            .FirstOrDefaultAsync(ct => ct.TmdbId == best.Id, cancellationToken);

        var tmdbOverview = detail?.Overview ?? best.Overview;
        var tmdbReleaseDate = detail?.ReleaseDate is not null
            ? DateTime.SpecifyKind(detail.ReleaseDate.Value, DateTimeKind.Utc)
            : (best.ReleaseDate is not null && DateTime.TryParse(best.ReleaseDate, out var rd)
                ? DateTime.SpecifyKind(rd, DateTimeKind.Utc)
                : (DateTime?)null);
        var tmdbRating = detail?.VoteAverage ?? best.VoteAverage;
        var tmdbGenres = detail?.Genres is { Count: > 0 }
            ? string.Join(", ", detail.Genres.Select(g => g.Name))
            : null;
        var tmdbTitle = detail?.Title ?? best.Title;

        if (canonicalTmdb is null)
        {
            canonicalTmdb = new CanonicalTmdbData
            {
                TmdbId = best.Id,
                TmdbTitle = tmdbTitle,
                Overview = tmdbOverview,
                ReleaseDate = tmdbReleaseDate,
                TmdbRating = tmdbRating,
                Genres = tmdbGenres,
                LastEnrichedAt = DateTime.UtcNow
            };
            _db.CanonicalTmdbData.Add(canonicalTmdb);
        }
        else
        {
            canonicalTmdb.TmdbTitle = tmdbTitle;
            canonicalTmdb.Overview = tmdbOverview;
            canonicalTmdb.ReleaseDate = tmdbReleaseDate;
            canonicalTmdb.TmdbRating = tmdbRating;
            canonicalTmdb.Genres = tmdbGenres;
            canonicalTmdb.LastEnrichedAt = DateTime.UtcNow;
            canonicalTmdb.UpdatedAt = DateTime.UtcNow;
        }

        // Phase 3: Download poster and store in content-addressed storage
        var posterPath = detail?.PosterPath ?? best.PosterPath;
        if (posterPath is not null && (canonicalTmdb.ExternalPosterHash is null || !_contentStorage.Exists(canonicalTmdb.ExternalPosterHash)))
        {
            var poster = await _tmdbClient.DownloadPosterAsync(posterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var ext = poster.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                canonicalTmdb.ExternalPosterHash = _contentStorage.Store(poster.Data, ext);
            }
        }

        // ── Mark the canonical video as TMDB-enriched ──
        // This flag is checked by the enrichment background queue to skip future runs,
        // and by the thumbnail/poster service to prefer the TMDB poster over screenshots.
        canonicalVideo.HasExternalPoster = true;
        canonicalVideo.ThumbnailPosterHash = null;
        // Propagate the poster hash from CanonicalTmdbData to CanonicalVideo so that
        // GetThumbnailAsync (Priority 2) can serve the poster via content-addressed storage.
        if (canonicalTmdb.ExternalPosterHash is not null)
            canonicalVideo.ExternalPosterHash = canonicalTmdb.ExternalPosterHash;
        canonicalVideo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} enriched from TMDB: {Title}", videoId, tmdbTitle);
    }



    [GeneratedRegex(@"(?<![0-9])(19[0-9]{2}|20[0-9]{2})(?![0-9])")]
    private static partial Regex YearRegex();

    /// <summary>
    /// Matches leading bracketed year prefixes like "[1982]" or "(1999)".
    /// </summary>
    [GeneratedRegex(@"^[[(]\d{4}[\])]\s*")]
    private static partial Regex LeadingBracketedYearRegex();

    /// <summary>
    /// Matches leading episode number patterns like "01 - ", "01 ", "01-".
    /// </summary>
    [GeneratedRegex(@"^\d{1,3}\s*[-–—]\s*|^\d{1,3}\s+")]
    private static partial Regex LeadingEpisodeNumRegex();

    /// <summary>
    /// Matches trailing encoding noise patterns like ".1987.TVRip.H264.AC3.DD2.0"
    /// anchored at the end: a dot followed by a 4-digit year then dot-separated quality tags.
    /// </summary>
    [GeneratedRegex(@"\.(?:19\d{2}|20\d{2})\.[A-Za-z0-9]+(?:\.[A-Za-z0-9]+)*$")]
    private static partial Regex TrailingEncodingNoiseRegex();

    /// <summary>
    /// Matches leading or trailing underscores (test file markers like "_test_").
    /// </summary>
    [GeneratedRegex(@"^_+|_+$")]
    private static partial Regex LeadingTrailingUnderscoreRegex();

    /// <summary>
    /// Cleans a raw video title string for use as a TMDB search query.
    /// Strips bracketed years, episode numbering, encoding noise, and formatting artifacts.
    /// </summary>
    private static string CleanSearchTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var result = raw;

        // Replace dots with spaces (common in EmbeddedTitle from ffprobe tags)
        result = result.Replace('.', ' ');

        // Remove trailing encoding noise: "1987 TVRip H264 AC3 DD2 0" after year
        // (dots were already replaced with spaces above)
        result = TrailingEncodingNoiseRegex().Replace(result, "");

        // Remove leading bracketed year: "[1982] Tron" → "Tron"
        result = LeadingBracketedYearRegex().Replace(result, "");

        // Remove leading episode numbers: "01 - After-Shock" → "After-Shock"
        result = LeadingEpisodeNumRegex().Replace(result, "");

        // Remove leading/trailing underscores (test files)
        result = LeadingTrailingUnderscoreRegex().Replace(result, "");

        // Collapse multiple spaces into one and trim
        result = string.Join(" ", result.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return result;
    }

    /// <summary>
    /// Searches TMDB for a movie by title (with optional year filter).
    /// Returns the first matching result, or null if no match found.
    /// The caller should fetch full details via <c>GetMovieAsync</c> on the returned ID.
    /// </summary>
    private async Task<TmdbMovieSearchResult?> TryMovieSearchAsync(
        string title, int? year, Guid videoId,
        CancellationToken cancellationToken)
    {
        var results = await _tmdbClient.SearchMovieAsync(title, year, cancellationToken);
        if (results is null || results.Count == 0)
            return null;

        var match = results[0];
        _logger.LogDebug("TMDB movie match for video {VideoId}: '{Title}' → '{MatchTitle}' (id={Id})",
            videoId, title, match.Title, match.Id);
        return match;
    }

    /// <summary>
    /// Searches TMDB for a TV series by name (with optional year filter).
    /// Returns a tuple with a movie-shaped search result and converted detail
    /// for consistent downstream processing (poster download, metadata storage).
    /// </summary>
    private async Task<(TmdbMovieSearchResult? SearchResult, TmdbMovieDetail? Detail)> TryTvSeriesSearchWithDetailAsync(
        string query, int? year, Guid videoId,
        CancellationToken cancellationToken)
    {
        var results = await _tmdbClient.SearchTvSeriesAsync(query, year, cancellationToken);
        if (results is null || results.Count == 0)
            return (null, null);

        var match = results[0];
        var tvDetail = await _tmdbClient.GetTvSeriesAsync(match.Id, cancellationToken);

        // Convert TV series result into movie-like format for downstream consistency
        var convertedDetail = tvDetail is not null
            ? new TmdbMovieDetail
            {
                Id = tvDetail.Id,
                Title = tvDetail.Name,
                Overview = tvDetail.Overview,
                PosterPath = tvDetail.PosterPath,
                ReleaseDate = tvDetail.Seasons is { Count: > 0 } && tvDetail.Seasons[0].AirDate is not null
                    ? DateTime.TryParse(tvDetail.Seasons[0].AirDate, out var d) ? d : null
                    : null,
                VoteAverage = tvDetail.VoteAverage,
                Genres = tvDetail.Genres
            }
            : null;

        _logger.LogDebug("TMDB TV series match for video {VideoId}: '{Query}' → '{SeriesName}' (id={Id})",
            videoId, query, match.Name, match.Id);

        var searchResult = new TmdbMovieSearchResult
        {
            Id = match.Id,
            Title = match.Name,
            Overview = match.Overview,
            PosterPath = match.PosterPath,
            ReleaseDate = match.FirstAirDate,
            VoteAverage = match.VoteAverage
        };

        return (searchResult, convertedDetail);
    }

    /// <summary>
    /// Checks if the given canonical video content hash belongs to a known series
    /// (movie franchise or TV series). Returns the series name if found, null otherwise.
    /// </summary>
    private async Task<string?> GetSeriesNameForVideoAsync(string contentHash, CancellationToken cancellationToken)
    {
        var seriesItem = await _db.CanonicalVideoSeriesItems
            .Include(si => si.Series)
            .FirstOrDefaultAsync(si => si.VideoContentHash == contentHash, cancellationToken);

        return seriesItem?.Series?.Name;
    }

    private static int? ExtractYear(string fileName)
    {
        var match = YearRegex().Match(fileName);
        if (match.Success && int.TryParse(match.Value, out var year))
            return year;
        return null;
    }

    private static bool TryExtractYearFromDate(string dateValue, out int year)
    {
        year = 0;
        if (string.IsNullOrWhiteSpace(dateValue))
            return false;

        // Handle formats: "1973", "1973-03-01", "1973-03", "1973/03/01"
        if (dateValue.Length >= 4 && int.TryParse(dateValue[..4], out var y) && y >= 1900 && y <= 2099)
        {
            year = y;
            return true;
        }

        // Fallback: try extracting a 4-digit year from anywhere in the string
        var match = YearRegex().Match(dateValue);
        if (match.Success && int.TryParse(match.Value, out year))
            return true;

        return false;
    }
}
