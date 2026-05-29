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
        var videoTitle = canonicalVideo.Title;

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
        if (searchResult is null)
        {
            var results = await _tmdbClient.SearchMovieAsync(videoTitle, year, cancellationToken);
            if (results is not null && results.Count > 0)
            {
                searchResult = results[0];
                detail = await _tmdbClient.GetMovieAsync(searchResult.Id, cancellationToken);
                _logger.LogDebug("TMDB match for '{Title}': {TmdbTitle} (id={Id})", videoTitle, searchResult.Title, searchResult.Id);
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

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} enriched from TMDB: {Title}", videoId, tmdbTitle);
    }



    [GeneratedRegex(@"(?<![0-9])(19[0-9]{2}|20[0-9]{2})(?![0-9])")]
    private static partial Regex YearRegex();

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
