using System.Text.RegularExpressions;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Orchestrates TMDB movie metadata and poster art enrichment for videos.
/// Writes enrichment data to <see cref="CanonicalTmdbData"/> with dual-write
/// to old Video entity fields for backward compatibility.
/// </summary>
public sealed partial class VideoEnrichmentService : IVideoEnrichmentService
{
    private readonly VideoDbContext _db;
    private readonly ITmdbClient _tmdbClient;
    private readonly IVideoSettingsProvider _settingsProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly string _posterCacheDir;
    private readonly ILogger<VideoEnrichmentService> _logger;

    private static readonly TimeSpan EnrichmentCooldown = TimeSpan.FromDays(30);

    public VideoEnrichmentService(VideoDbContext db, ITmdbClient tmdbClient, IVideoSettingsProvider settingsProvider, IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<VideoEnrichmentService> logger)
    {
        _db = db;
        _tmdbClient = tmdbClient;
        _settingsProvider = settingsProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Check file config as a startup fallback — runtime DB check happens in enrichment methods
        IsTmdbAvailable = !string.IsNullOrWhiteSpace(configuration["Video:Enrichment:TmdbApiKey"]);

        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _posterCacheDir = Path.Combine(storageRoot, ".video-posters");
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

        var oldVideo = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken);

        CanonicalVideo? canonicalVideo = userVideo?.CanonicalVideo;
        if (canonicalVideo is null && oldVideo is null)
            return;

        var videoTitle = canonicalVideo?.Title ?? oldVideo?.Title ?? string.Empty;
        var videoFileName = oldVideo?.FileName ?? string.Empty;

        if (oldVideo is not null && !force && oldVideo.LastEnrichedAt is not null && DateTime.UtcNow - oldVideo.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Video {VideoId} enriched recently, skipping (cooldown)", videoId);
            return;
        }

        // Source year from embedded metadata first, then folder context, then filename regex
        var year = canonicalVideo?.EmbeddedDate is not null && TryExtractYearFromDate(canonicalVideo.EmbeddedDate, out var embeddedYear)
            ? embeddedYear
            : ExtractYear(videoFileName);

        // ── Priority-based TMDB lookup ──

        TmdbMovieDetail? detail = null;
        TmdbMovieSearchResult? searchResult = null;

        // Priority 1: Direct TMDB ID lookup if embedded
        if (canonicalVideo?.EmbeddedTmdbId is not null)
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
        if (searchResult is null && canonicalVideo?.EmbeddedImdbId is not null)
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
            if (oldVideo is not null)
            {
                oldVideo.LastEnrichedAt = DateTime.UtcNow;
                oldVideo.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(cancellationToken);
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
        string? externalPosterHash = null;
        if (posterPath is not null && (canonicalTmdb.ExternalPosterHash is null || !_contentStorage.Exists(canonicalTmdb.ExternalPosterHash)))
        {
            var poster = await _tmdbClient.DownloadPosterAsync(posterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var ext = poster.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
                externalPosterHash = _contentStorage.Store(poster.Data, ext);
                canonicalTmdb.ExternalPosterHash = externalPosterHash;

                // Also cache on disk for old-style access
                var cachePath = CacheExternalPoster(poster.Data, poster.MimeType, videoId);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // ── Dual-write: update old Video fields ──
        if (oldVideo is not null)
        {
            oldVideo.TmdbId = best.Id;
            oldVideo.TmdbTitle = tmdbTitle;
            oldVideo.Overview = tmdbOverview;
            oldVideo.ReleaseDate = tmdbReleaseDate;
            oldVideo.TmdbRating = tmdbRating;
            oldVideo.Genres = tmdbGenres;
            oldVideo.HasExternalPoster = canonicalTmdb.ExternalPosterHash is not null || externalPosterHash is not null;
            oldVideo.LastEnrichedAt = DateTime.UtcNow;
            oldVideo.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Phase 4: Check if the movie belongs to a TMDB collection (franchise)
        if (detail?.BelongsToCollection is not null && oldVideo is not null)
        {
            await AssignVideoToFranchiseAsync(oldVideo, detail.BelongsToCollection, caller, cancellationToken);
        }

        _logger.LogInformation("Video {VideoId} enriched from TMDB: {Title}", videoId, tmdbTitle);
    }

    /// <summary>
    /// Assigns a video to a movie franchise series based on TMDB collection data.
    /// </summary>
    private async Task AssignVideoToFranchiseAsync(Models.Video video, TmdbCollectionInfo collection, CallerContext caller, CancellationToken cancellationToken)
    {
        try
        {
            // Use a scope to get a properly-resolved VideoSeriesService with its own logger
            await using var scope = _scopeFactory.CreateAsyncScope();
            var seriesService = scope.ServiceProvider.GetRequiredService<IVideoSeriesService>();

            // Find or create a MovieFranchise series using the collection name
            var series = await seriesService.FindOrCreateByNameAsync(collection.Name ?? "Unknown Collection", "MovieFranchise", caller, cancellationToken);

            // Check if video is already in this series
            var alreadyInSeries = await _db.VideoSeriesItems
                .AnyAsync(i => i.SeriesId == Guid.Parse(series.Id.ToString()) && i.VideoId == video.Id, cancellationToken);

            if (alreadyInSeries)
                return;

            // Get the collection details to find this movie's sort order among parts
            var collectionDetail = await _tmdbClient.GetCollectionAsync(collection.Id, cancellationToken);
            var partIndex = 0;
            if (collectionDetail is not null)
            {
                var part = collectionDetail.Parts
                    .Select((p, idx) => new { p.Id, Index = idx })
                    .FirstOrDefault(p => p.Id == video.TmdbId);
                if (part is not null)
                    partIndex = part.Index;
            }

            await seriesService.AddVideoToSeriesAsync(
                Guid.Parse(series.Id.ToString()),
                video.Id,
                partIndex > 0 ? partIndex : null,
                video.TmdbTitle,
                caller,
                cancellationToken);

            // Download the collection poster for the series if not already set
            if (collectionDetail?.PosterPath is not null)
            {
                var seriesEntity = await _db.VideoSeries
                    .FirstOrDefaultAsync(s => s.Id == Guid.Parse(series.Id.ToString()), cancellationToken);

                if (seriesEntity is not null && !seriesEntity.HasExternalPoster)
                {
                    var poster = await _tmdbClient.DownloadPosterAsync(collectionDetail.PosterPath, cancellationToken: cancellationToken);
                    if (poster is not null)
                    {
                        var cachePath = CacheExternalPoster(poster.Data, poster.MimeType, seriesEntity.Id);
                        if (cachePath is not null)
                        {
                            seriesEntity.HasExternalPoster = true;
                            seriesEntity.ExternalPosterPath = cachePath;
                            seriesEntity.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Video {VideoId} ('{Title}') assigned to movie franchise '{SeriesName}' at position {Position}",
                video.Id, video.TmdbTitle ?? video.Title, collection.Name, partIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to assign video {VideoId} to TMDB collection '{CollectionName}'",
                video.Id, collection.Name);
        }
    }

    /// <inheritdoc />
    public async Task EnrichVideosWithoutPosterAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Where(v => v.OwnerId == ownerId && !v.HasExternalPoster)
            .OrderBy(v => v.Title)
            .ToListAsync(cancellationToken);

        var total = videos.Count;
        var found = 0;

        for (var i = 0; i < videos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var caller = new CallerContext(ownerId, ["user"], CallerType.System);
            await EnrichVideoAsync(videos[i].Id, caller, cancellationToken: cancellationToken);

            // Reload to check if poster was found
            var reloaded = await _db.Videos
                .Where(v => v.Id == videos[i].Id)
                .Select(v => new { v.HasExternalPoster })
                .FirstOrDefaultAsync(cancellationToken);

            if (reloaded?.HasExternalPoster == true)
                found++;

            progress?.Report(new EnrichmentProgress
            {
                Phase = "Fetching posters...",
                Current = i + 1,
                Total = total,
                CurrentItem = videos[i].Title,
                AlbumArtFound = found,
                AlbumArtRemaining = total - (i + 1)
            });
        }
    }

    /// <inheritdoc />
    public async Task EnrichAllAsync(Guid ownerId, IProgress<EnrichmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var videos = await _db.Videos
            .Where(v => v.OwnerId == ownerId && v.LastEnrichedAt == null)
            .OrderBy(v => v.Title)
            .ToListAsync(cancellationToken);

        var total = videos.Count;
        var enriched = 0;

        for (var i = 0; i < videos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var caller = new CallerContext(ownerId, ["user"], CallerType.System);
            await EnrichVideoAsync(videos[i].Id, caller, cancellationToken: cancellationToken);
            enriched++;

            progress?.Report(new EnrichmentProgress
            {
                Phase = "Enriching videos...",
                Current = i + 1,
                Total = total,
                CurrentItem = videos[i].Title,
                AlbumArtFound = enriched,
                AlbumArtRemaining = total - (i + 1)
            });
        }
    }

    // ─── Series Enrichment ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task EnrichSeriesAsync(Guid seriesId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var series = await _db.VideoSeries
            .Include(s => s.Seasons)
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.OwnerId == caller.UserId, cancellationToken);

        if (series is null)
            throw new BusinessRuleException(ErrorCodes.VideoSeriesNotFound, "Series not found.");

        var tmdbAvailable = await _settingsProvider.IsTmdbAvailableAsync(cancellationToken);
        if (!tmdbAvailable)
        {
            _logger.LogDebug("TMDB not available, skipping enrichment for series {SeriesId}", seriesId);
            return;
        }

        // Enrich based on series type
        if (series.Type == Models.SeriesType.TvSeries)
            await EnrichTvSeriesAsync(series, caller, cancellationToken);
        else
            await EnrichMovieFranchiseAsync(series, caller, cancellationToken);

        series.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Series {SeriesId} '{Name}' enriched from TMDB", seriesId, series.Name);
    }

    /// <summary>
    /// Enriches a TV series from TMDB — fetches overview, rating, seasons, poster,
    /// and maps user episode videos to canonical episodes by episode number.
    /// </summary>
    private async Task EnrichTvSeriesAsync(VideoSeries series, CallerContext caller, CancellationToken cancellationToken)
    {
        // Try to determine year from series name or first existing episode
        int? seriesYear = null;
        var seasonEntities = await _db.VideoSeasons
            .Where(vs => vs.SeriesId == series.Id)
            .OrderBy(vs => vs.SeasonNumber)
            .ToListAsync(cancellationToken);

        if (seasonEntities.Count > 0)
        {
            var firstSeason = seasonEntities[0];
            var episodeEntities = await _db.VideoEpisodes
                .Where(ve => ve.SeasonId == firstSeason.Id)
                .OrderBy(ve => ve.EpisodeNumber)
                .Take(1)
                .ToListAsync(cancellationToken);

            if (episodeEntities.Count > 0)
            {
                var firstEpisode = episodeEntities[0];
                var firstVideo = await _db.Videos
                    .FirstOrDefaultAsync(v => v.Id == firstEpisode.VideoId, cancellationToken);

                if (firstVideo?.ContentHash is not null)
                {
                    var firstUserVideo = await _db.UserVideos
                        .Include(uv => uv.CanonicalVideo)
                        .FirstOrDefaultAsync(uv => uv.CanonicalContentHash == firstVideo.ContentHash, cancellationToken);

                    if (firstUserVideo?.CanonicalVideo is not null)
                    {
                        seriesYear = ExtractYear(firstUserVideo.CanonicalVideo.FileName);
                        if (seriesYear is null && firstUserVideo.CanonicalVideo.EmbeddedDate is not null)
                        {
                            TryExtractYearFromDate(firstUserVideo.CanonicalVideo.EmbeddedDate, out var y);
                            seriesYear = y > 0 ? y : null;
                        }
                    }
                }
            }
        }

        // Search for the series by name with year filter
        var results = await _tmdbClient.SearchTvSeriesAsync(series.Name, seriesYear, cancellationToken);
        if (results is null || results.Count == 0)
        {
            _logger.LogDebug("No TMDB TV series results found for '{Name}'", series.Name);
            return;
        }

        var best = results[0];

        // Get full details
        var detail = await _tmdbClient.GetTvSeriesAsync(best.Id, cancellationToken);
        if (detail is null)
        {
            _logger.LogDebug("TMDB TV series detail unavailable for {TmdbId}", best.Id);
            return;
        }

        series.TmdbId = detail.Id;
        series.TmdbName = detail.Name;
        series.TmdbOverview = detail.Overview;
        series.TmdbRating = detail.VoteAverage;
        series.Genres = detail.Genres.Count > 0 ? string.Join(", ", detail.Genres.Select(g => g.Name)) : null;
        series.Status = detail.Status;
        series.TotalSeasons = detail.NumberOfSeasons;
        series.TotalEpisodes = detail.NumberOfEpisodes;

        // Download poster
        var posterPath = detail.PosterPath ?? best.PosterPath;
        if (posterPath is not null && (!series.HasExternalPoster || !File.Exists(series.ExternalPosterPath)))
        {
            var poster = await _tmdbClient.DownloadPosterAsync(posterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var cachePath = CacheExternalPoster(poster.Data, poster.MimeType, series.Id);
                if (cachePath is not null)
                {
                    series.HasExternalPoster = true;
                    series.ExternalPosterPath = cachePath;
                }
            }
        }

        // ── Episode matching: map user videos to canonical episodes ──

        // Find or create canonical series
        var canonicalSeries = await _db.CanonicalVideoSeries
            .FirstOrDefaultAsync(cvs => cvs.Name == series.Name, cancellationToken);

        if (canonicalSeries is null)
        {
            canonicalSeries = new CanonicalVideoSeries
            {
                Name = series.Name,
                Description = series.TmdbOverview,
                Type = SeriesType.TvSeries,
                TmdbId = detail.Id,
                TmdbName = detail.Name,
                TmdbOverview = detail.Overview,
                TmdbRating = detail.VoteAverage,
                Genres = series.Genres,
                Status = detail.Status,
                TotalSeasons = detail.NumberOfSeasons,
                TotalEpisodes = detail.NumberOfEpisodes
            };
            _db.CanonicalVideoSeries.Add(canonicalSeries);
            await _db.SaveChangesAsync(cancellationToken);
        }

        foreach (var season in detail.Seasons)
        {
            if (season.SeasonNumber <= 0)
                continue; // Skip "specials" or unnumbered seasons

            var seasonDetail = await _tmdbClient.GetTvSeasonAsync(detail.Id, season.SeasonNumber, cancellationToken);
            if (seasonDetail?.Episodes is null || seasonDetail.Episodes.Count == 0)
                continue;

            // Find or create canonical season
            var canonicalSeason = await _db.CanonicalVideoSeasons
                .FirstOrDefaultAsync(cs => cs.SeriesId == canonicalSeries.Id && cs.SeasonNumber == season.SeasonNumber, cancellationToken);

            if (canonicalSeason is null)
            {
                canonicalSeason = new CanonicalVideoSeason
                {
                    SeriesId = canonicalSeries.Id,
                    SeasonNumber = season.SeasonNumber,
                    Name = season.Name ?? $"Season {season.SeasonNumber}",
                    Overview = season.Overview,
                    EpisodeCount = season.EpisodeCount
                };
                _db.CanonicalVideoSeasons.Add(canonicalSeason);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Match each local season's episode videos to TMDB episodes
            var localSeason = seasonEntities.FirstOrDefault(vs => vs.SeasonNumber == season.SeasonNumber);

            if (localSeason is not null)
            {
                var localEpisodes = await _db.VideoEpisodes
                    .Where(ve => ve.SeasonId == localSeason.Id)
                    .ToListAsync(cancellationToken);

                foreach (var localEpisode in localEpisodes)
                {
                    var tmdbEpisode = seasonDetail.Episodes
                        .FirstOrDefault(e => e.EpisodeNumber == localEpisode.EpisodeNumber);

                    if (tmdbEpisode is null)
                        continue;

                    // Resolve content hash from old Video -> UserVideo
                    var episodeOldVideo = await _db.Videos
                        .FirstOrDefaultAsync(v => v.Id == localEpisode.VideoId, cancellationToken);

                    if (episodeOldVideo?.ContentHash is null)
                        continue;

                    var episodeUserVideo = await _db.UserVideos
                        .FirstOrDefaultAsync(uv => uv.CanonicalContentHash == episodeOldVideo.ContentHash, cancellationToken);

                    if (episodeUserVideo is null)
                        continue;

                    // Find or create canonical episode record
                    var canonicalEpisode = await _db.CanonicalVideoEpisodes
                        .FirstOrDefaultAsync(ce => ce.SeasonId == canonicalSeason.Id && ce.EpisodeNumber == localEpisode.EpisodeNumber, cancellationToken);

                    if (canonicalEpisode is null)
                    {
                        canonicalEpisode = new CanonicalVideoEpisode
                        {
                            SeasonId = canonicalSeason.Id,
                            VideoContentHash = episodeUserVideo.CanonicalContentHash,
                            EpisodeNumber = localEpisode.EpisodeNumber,
                            Title = tmdbEpisode.Name ?? localEpisode.Title,
                            Overview = tmdbEpisode.Overview,
                            SortOrder = localEpisode.EpisodeNumber
                        };
                        _db.CanonicalVideoEpisodes.Add(canonicalEpisode);
                    }
                    else
                    {
                        // Update the content hash if a different user shares the same episode file
                        canonicalEpisode.VideoContentHash = episodeUserVideo.CanonicalContentHash;
                        canonicalEpisode.Title ??= tmdbEpisode.Name ?? localEpisode.Title;
                        canonicalEpisode.Overview ??= tmdbEpisode.Overview;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Enriches a movie franchise from TMDB — fetches collection overview, parts, poster.
    /// </summary>
    private async Task EnrichMovieFranchiseAsync(VideoSeries series, CallerContext caller, CancellationToken cancellationToken)
    {
        // Search for the collection by name
        var results = await _tmdbClient.SearchCollectionAsync(series.Name, cancellationToken);
        if (results is null || results.Count == 0)
        {
            _logger.LogDebug("No TMDB collection results found for '{Name}'", series.Name);
            return;
        }

        var best = results[0];

        // Get full collection details
        var detail = await _tmdbClient.GetCollectionAsync(best.Id, cancellationToken);
        if (detail is null)
        {
            _logger.LogDebug("TMDB collection detail unavailable for {TmdbId}", best.Id);
            return;
        }

        series.TmdbId = detail.Id;
        series.TmdbName = detail.Name;
        series.TmdbOverview = detail.Overview;
        series.TotalEpisodes = detail.Parts.Count;

        // Download poster
        var posterPath = detail.PosterPath ?? best.PosterPath;
        if (posterPath is not null && (!series.HasExternalPoster || !File.Exists(series.ExternalPosterPath)))
        {
            var poster = await _tmdbClient.DownloadPosterAsync(posterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var cachePath = CacheExternalPoster(poster.Data, poster.MimeType, series.Id);
                if (cachePath is not null)
                {
                    series.HasExternalPoster = true;
                    series.ExternalPosterPath = cachePath;
                }
            }
        }
    }

    private string? CacheExternalPoster(byte[] data, string mimeType, Guid videoId)
    {
        try
        {
            Directory.CreateDirectory(_posterCacheDir);
            var ext = mimeType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            var path = Path.Combine(_posterCacheDir, $"{videoId}{ext}");
            File.WriteAllBytes(path, data);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache poster for video {VideoId}", videoId);
            return null;
        }
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
