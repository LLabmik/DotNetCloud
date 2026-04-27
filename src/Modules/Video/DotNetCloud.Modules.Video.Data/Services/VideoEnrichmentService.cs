using System.Text.RegularExpressions;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Orchestrates TMDB movie metadata and poster art enrichment for videos.
/// </summary>
public sealed partial class VideoEnrichmentService : IVideoEnrichmentService
{
    private readonly VideoDbContext _db;
    private readonly ITmdbClient _tmdbClient;
    private readonly string _posterCacheDir;
    private readonly ILogger<VideoEnrichmentService> _logger;

    private static readonly TimeSpan EnrichmentCooldown = TimeSpan.FromDays(30);

    public VideoEnrichmentService(VideoDbContext db, ITmdbClient tmdbClient, IConfiguration configuration, ILogger<VideoEnrichmentService> logger)
    {
        _db = db;
        _tmdbClient = tmdbClient;
        _logger = logger;

        IsTmdbAvailable = !string.IsNullOrWhiteSpace(configuration["Video:Enrichment:TmdbApiKey"]);

        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _posterCacheDir = Path.Combine(storageRoot, ".video-posters");
    }

    /// <inheritdoc />
    public bool IsTmdbAvailable { get; }

    /// <inheritdoc />
    public async Task EnrichVideoAsync(Guid videoId, CallerContext caller, bool force = false, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .Where(v => v.Id == videoId && v.OwnerId == caller.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (video is null)
            return;

        if (!force && video.LastEnrichedAt is not null && DateTime.UtcNow - video.LastEnrichedAt.Value < EnrichmentCooldown)
        {
            _logger.LogDebug("Video {VideoId} enriched recently, skipping (cooldown)", videoId);
            return;
        }

        var year = ExtractYear(video.FileName);

        // Phase 1: Search TMDB
        var results = await _tmdbClient.SearchMovieAsync(video.Title, year, cancellationToken);
        if (results is null || results.Count == 0)
        {
            _logger.LogDebug("No TMDB results found for video {VideoId} ('{Title}')", videoId, video.Title);
            video.LastEnrichedAt = DateTime.UtcNow;
            video.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var best = results[0];
        _logger.LogDebug("TMDB match for '{Title}': {TmdbTitle} (score={Id})", video.Title, best.Title, best.Id);

        // Phase 2: Get full details
        var detail = await _tmdbClient.GetMovieAsync(best.Id, cancellationToken);
        if (detail is not null)
        {
            video.TmdbId = detail.Id;
            video.TmdbTitle = detail.Title;
            video.Overview = detail.Overview;
            video.ReleaseDate = detail.ReleaseDate is not null ? DateTime.SpecifyKind(detail.ReleaseDate.Value, DateTimeKind.Utc) : null;
            video.TmdbRating = detail.VoteAverage;
            video.Genres = detail.Genres.Count > 0 ? string.Join(", ", detail.Genres.Select(g => g.Name)) : null;
        }
        else
        {
            _logger.LogDebug("TMDB movie detail unavailable for {TmdbId}, falling back to search result", best.Id);
            // Use search result as fallback
            video.TmdbId = best.Id;
            video.TmdbTitle = best.Title;
            video.Overview = best.Overview;
            video.TmdbRating = best.VoteAverage;
            if (best.ReleaseDate is not null && DateTime.TryParse(best.ReleaseDate, out var rd))
                video.ReleaseDate = DateTime.SpecifyKind(rd, DateTimeKind.Utc);
        }

        // Phase 3: Download poster
        var posterPath = detail?.PosterPath ?? best.PosterPath;
        if (posterPath is not null && (!video.HasExternalPoster || !File.Exists(video.ExternalPosterPath)))
        {
            var poster = await _tmdbClient.DownloadPosterAsync(posterPath, cancellationToken: cancellationToken);
            if (poster is not null)
            {
                var cachePath = CacheExternalPoster(poster.Data, poster.MimeType, video.Id);
                if (cachePath is not null)
                {
                    video.HasExternalPoster = true;
                    video.ExternalPosterPath = cachePath;
                }
            }
        }

        video.LastEnrichedAt = DateTime.UtcNow;
        video.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video {VideoId} enriched from TMDB: {Title}", videoId, video.TmdbTitle ?? video.Title);
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

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    private static int? ExtractYear(string fileName)
    {
        var match = YearRegex().Match(fileName);
        if (match.Success && int.TryParse(match.Value, out var year))
            return year;
        return null;
    }
}
