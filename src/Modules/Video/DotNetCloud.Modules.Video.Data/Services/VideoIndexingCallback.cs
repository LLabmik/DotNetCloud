using System.Text.RegularExpressions;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Events;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Implements the video indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedVideoHandler when a video file is uploaded.
/// </summary>
public sealed class VideoIndexingCallback : IVideoIndexingCallback
{
    private readonly VideoService _videoService;
    private readonly IVideoCollectionService _collectionService;
    private readonly IVideoSeriesService _seriesService;
    private readonly VideoDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoIndexingCallback> _logger;

    private static readonly Regex TvSeriesPattern = new(
        @"^(.+?)[._\s]+[Ss](\d{1,2})[Ee](\d{1,3})",
        RegexOptions.Compiled);

    private static readonly Regex SeasonFolderPattern = new(
        @"[Ss]eason[\s._]*(\d{1,2})",
        RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoIndexingCallback"/> class.
    /// </summary>
    public VideoIndexingCallback(VideoService videoService, IVideoCollectionService collectionService, IVideoSeriesService seriesService, VideoDbContext db, IConfiguration configuration, ILogger<VideoIndexingCallback> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _seriesService = seriesService;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Canonical/user-junction deduplication is handled internally by <see cref="VideoService.CreateVideoAsync"/>.
    /// Cross-owner dedup is automatic via ContentHash lookup. The callback handles
    /// collection assignment and series auto-detection. TMDB enrichment and metadata
    /// extraction run as a batch post-scan via <see cref="VideoEnrichmentBackgroundService"/>.
    /// </remarks>
    public async Task IndexVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, string? storagePath = null, string? sourceName = null, string? subFolderPath = null, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        var video = await _videoService.CreateVideoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        // ── Source-based collection assignment ──
        // When the scan pipeline provides a source name, ensure a collection with that
        // name exists for this owner and add the newly indexed video to it.
        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            try
            {
                var collection = await _collectionService.FindOrCreateByNameAsync(sourceName, caller, cancellationToken);
                await _collectionService.AddVideoAsync(collection.Id, video.Id, caller, cancellationToken);
                _logger.LogDebug(
                    "Video {VideoId} assigned to source collection '{CollectionName}' ({CollectionId})",
                    video.Id, sourceName, collection.Id);
            }
            catch (Exception ex)
            {
                // Graceful failure — video indexing itself succeeded, collection assignment is best-effort.
                _logger.LogWarning(ex,
                    "Failed to assign video {VideoId} to source collection '{SourceName}'",
                    video.Id, sourceName);
            }
        }

        // ── Series auto-detection ──
        // Detect TV series patterns from file path and assign the video to a series/season.
        // Prefer subFolderPath (preserves original directory structure from scan) over
        // storagePath (content-addressable, not useful for hierarchy parsing) and fileName.
        // Construct full relative path including file name so directory-based detection works.
        var seriesPath = subFolderPath is not null
            ? $"{subFolderPath}/{fileName}"
            : storagePath ?? fileName;
        if (!string.IsNullOrWhiteSpace(seriesPath))
        {
            await AutoDetectSeriesAsync(video.Id, seriesPath, caller, cancellationToken);
        }

        _logger.LogDebug("Video indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }

    /// <inheritdoc />
    public async Task ResetCollectionAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RESET: Deleting video library metadata for owner {OwnerId}", ownerId);

        // Clean up screenshot and poster cache directories
        var storageRoot = _configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        foreach (var dir in new[] { ".video-screenshots", ".video-posters" })
        {
            var path = Path.Combine(storageRoot, dir);
            if (Directory.Exists(path))
            {
                try
                { Directory.Delete(path, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete directory {Path} during collection reset", path); }
            }
        }

        // ── Clear per-user collections and their items ──
        var collections = await _db.UserVideoCollections
            .Where(c => c.OwnerId == ownerId).ToListAsync(cancellationToken);
        var collectionIds = collections.Select(c => c.Id).ToList();
        var collectionItems = await _db.UserVideoCollectionItems
            .Where(ci => collectionIds.Contains(ci.CollectionId)).ToListAsync(cancellationToken);
        _db.UserVideoCollectionItems.RemoveRange(collectionItems);
        _db.UserVideoCollections.RemoveRange(collections);

        // ── Clear per-user video junctions ──
        var userVideos = await _db.UserVideos.IgnoreQueryFilters()
            .Where(uv => uv.OwnerId == ownerId).ToListAsync(cancellationToken);
        var userVideoIds = userVideos.Select(uv => uv.Id).ToList();
        _db.UserVideos.RemoveRange(userVideos);

        // ── Clear watch progress and history for this user ──
        var watchProgress = await _db.WatchProgresses
            .Where(wp => wp.UserId == ownerId).ToListAsync(cancellationToken);
        _db.WatchProgresses.RemoveRange(watchProgress);

        var watchHistory = await _db.WatchHistories
            .Where(wh => wh.UserId == ownerId).ToListAsync(cancellationToken);
        _db.WatchHistories.RemoveRange(watchHistory);

        // ── Clear video shares by this user ──
        var shares = await _db.VideoShares
            .Where(vs => vs.SharedByUserId == ownerId).ToListAsync(cancellationToken);
        _db.VideoShares.RemoveRange(shares);

        // ── Clear legacy per-user data ──
        var legacyVideos = await _db.Videos
            .Where(v => v.OwnerId == ownerId).ToListAsync(cancellationToken);
        _db.Videos.RemoveRange(legacyVideos);

        var legacyCollections = await _db.VideoCollections
            .Where(vc => vc.OwnerId == ownerId).ToListAsync(cancellationToken);
        var legacyCollectionIds = legacyCollections.Select(c => c.Id).ToList();
        var legacyCollectionItems = await _db.VideoCollectionItems
            .Where(ci => legacyCollectionIds.Contains(ci.CollectionId)).ToListAsync(cancellationToken);
        _db.VideoCollectionItems.RemoveRange(legacyCollectionItems);
        _db.VideoCollections.RemoveRange(legacyCollections);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "RESET complete for owner {OwnerId}: {UserVideoCount} user videos, {CollectionCount} collections, " +
            "{ProgressCount} watch progress, {HistoryCount} watch history, {ShareCount} shares cleared",
            ownerId, userVideos.Count, collections.Count, watchProgress.Count, watchHistory.Count, shares.Count);
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var ids = await _db.UserVideos
            .Where(uv => uv.OwnerId == ownerId)
            .Select(uv => uv.FileNodeId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    /// <inheritdoc />
    public async Task<int> RemoveDeletedVideosAsync(IReadOnlyCollection<Guid> deletedFileNodeIds, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var videos = await _db.UserVideos
            .Where(uv => uv.OwnerId == ownerId && deletedFileNodeIds.Contains(uv.FileNodeId) && !uv.IsDeleted)
            .ToListAsync(cancellationToken);

        if (videos.Count == 0)
            return 0;

        // Soft-delete the user video records.
        var now = DateTime.UtcNow;
        foreach (var uv in videos)
        {
            uv.IsDeleted = true;
            uv.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Removed {Count} deleted video records for user {OwnerId}",
            videos.Count, ownerId);

        return videos.Count;
    }

    /// <summary>
    /// Attempts to auto-detect a TV series and season from the video's storage path or filename,
    /// and assigns the video to the detected series/season.
    /// </summary>
    private async Task AutoDetectSeriesAsync(Guid videoId, string path, CallerContext caller, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize path separators
            var normalizedPath = path.Replace('\\', '/');
            var fileName = System.IO.Path.GetFileNameWithoutExtension(normalizedPath);

            // Try filename pattern: "Series.Name.S01E01.ext"
            var tvMatch = TvSeriesPattern.Match(fileName);
            if (!tvMatch.Success)
            {
                // Try subfolder pattern: look for "Season 01" or "S01" in the path
                var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 1; i < segments.Length; i++)
                {
                    var folderSeason = SeasonFolderPattern.Match(segments[i]);
                    if (folderSeason.Success && i > 0)
                    {
                        // The parent folder is the series name
                        var seriesName = CleanSeriesName(segments[i - 1]);
                        var seasonNumber = int.Parse(folderSeason.Groups[1].Value);

                        var series = await _seriesService.FindOrCreateByNameAsync(seriesName, "TvSeries", caller, cancellationToken);
                        var season = await _seriesService.FindOrCreateSeasonAsync(Guid.Parse(series.Id.ToString()), seasonNumber, null, caller, cancellationToken);

                        // Assign an auto-incrementing episode number to avoid unique constraint violations
                        var nextEpisodeNum = await _db.CanonicalVideoEpisodes
                            .Where(e => e.SeasonId == Guid.Parse(season.Id.ToString()))
                            .MaxAsync(e => (int?)e.EpisodeNumber, cancellationToken) ?? 0;
                        nextEpisodeNum++;

                        // Look up the canonical video title to use as episode title
                        var episodeTitle = await _db.UserVideos
                            .Where(uv => uv.Id == videoId && uv.CanonicalVideo != null)
                            .Select(uv => uv.CanonicalVideo!.Title)
                            .FirstOrDefaultAsync(cancellationToken);

                        await _seriesService.AddEpisodeAsync(
                            Guid.Parse(season.Id.ToString()), videoId, nextEpisodeNum, episodeTitle, null, caller, cancellationToken);

                        _logger.LogInformation(
                            "Video {VideoId} auto-assigned to series '{SeriesName}', season {SeasonNumber}",
                            videoId, seriesName, seasonNumber);

                        // Fire-and-forget TMDB enrichment for this series to fetch posters/metadata
                        EnrichSeriesInBackground(Guid.Parse(series.Id.ToString()), caller.UserId);
                        return;
                    }
                }

                // ── Movie franchise fallback: if the video is in a subfolder, use parent folder name ──
                if (segments.Length >= 2)
                {
                    // The file is at least one level deep — use the immediate parent folder as franchise name
                    // Skip library root folders (e.g., "Movies", "Videos", "_DotNetCloud")
                    var parentFolder = segments[^2];
                    var folderNameLower = parentFolder.ToLowerInvariant();

                    // Don't treat generic folders as franchise names
                    var genericFolders = new HashSet<string> { "movies", "videos", "video", "films", "library", "media", "content", "_dotnetcloud", "files" };
                    if (!genericFolders.Contains(folderNameLower))
                    {
                        var franchiseName = CleanSeriesName(parentFolder);

                        var series = await _seriesService.FindOrCreateByNameAsync(franchiseName, "MovieFranchise", caller, cancellationToken);

                        // Check if already in series (via canonical content hash)
                        var contentHash = await _db.UserVideos
                            .Where(uv => uv.Id == videoId)
                            .Select(uv => uv.CanonicalContentHash)
                            .FirstOrDefaultAsync(cancellationToken);

                        var alreadyInSeries = false;
                        if (contentHash is not null)
                        {
                            alreadyInSeries = await _db.CanonicalVideoSeriesItems
                                .AnyAsync(i => i.SeriesId == Guid.Parse(series.Id.ToString()) && i.VideoContentHash == contentHash, cancellationToken);
                        }

                        if (!alreadyInSeries)
                        {
                            // Get the next sort order
                            var maxOrder = await _db.CanonicalVideoSeriesItems
                                .Where(i => i.SeriesId == Guid.Parse(series.Id.ToString()))
                                .MaxAsync(i => (int?)i.SortOrder, cancellationToken) ?? -1;

                            // Look up the canonical video title to use as episode title
                            var episodeTitle = await _db.UserVideos
                                .Where(uv => uv.Id == videoId && uv.CanonicalVideo != null)
                                .Select(uv => uv.CanonicalVideo!.Title)
                                .FirstOrDefaultAsync(cancellationToken);

                            await _seriesService.AddVideoToSeriesAsync(
                                Guid.Parse(series.Id.ToString()), videoId, maxOrder + 1, episodeTitle, caller, cancellationToken);

                            _logger.LogInformation(
                                "Video {VideoId} auto-assigned to movie franchise '{FranchiseName}' (folder-based)",
                                videoId, franchiseName);

                            // Fire-and-forget TMDB enrichment for this series to fetch posters/metadata
                            EnrichSeriesInBackground(Guid.Parse(series.Id.ToString()), caller.UserId);
                        }
                        return;
                    }
                }
                return;
            }

            // Extract series name, season, and episode
            var rawSeriesName = tvMatch.Groups[1].Value;
            var seasonNum = int.Parse(tvMatch.Groups[2].Value);
            var episodeNum = int.Parse(tvMatch.Groups[3].Value);
            var seriesNameClean = CleanSeriesName(rawSeriesName);

            // Find or create the series (default to TvSeries type)
            var seriesDto = await _seriesService.FindOrCreateByNameAsync(seriesNameClean, "TvSeries", caller, cancellationToken);

            // Find or create the season
            var seasonDto = await _seriesService.FindOrCreateSeasonAsync(
                Guid.Parse(seriesDto.Id.ToString()), seasonNum, null, caller, cancellationToken);

            // Add the video as an episode
            await _seriesService.AddEpisodeAsync(
                Guid.Parse(seasonDto.Id.ToString()), videoId, episodeNum, null, null, caller, cancellationToken);

            _logger.LogInformation(
                "Video {VideoId} auto-assigned to series '{SeriesName}', season {SeasonNumber}, episode {EpisodeNumber}",
                videoId, seriesNameClean, seasonNum, episodeNum);

            // Fire-and-forget TMDB enrichment for this series to fetch posters/metadata
            EnrichSeriesInBackground(Guid.Parse(seriesDto.Id.ToString()), caller.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Series auto-detection failed for video {VideoId}", videoId);
        }
    }

    // Limits concurrent series enrichments during library scan to avoid overwhelming the TMDB API.
    private static readonly System.Threading.SemaphoreSlim _seriesEnrichmentThrottle = new(3, 3);

    /// <summary>
    /// Fires TMDB enrichment for a series to fetch posters and metadata.
    /// Runs as a short-lived background task that doesn't block the indexing pipeline.
    /// Concurrency is throttled to 3 simultaneous enrichments to avoid overwhelming the TMDB API.
    /// </summary>
    private void EnrichSeriesInBackground(Guid seriesId, Guid ownerId)
    {
        _ = Task.Run(async () =>
        {
            await _seriesEnrichmentThrottle.WaitAsync();
            try
            {
                await _seriesService.EnrichSeriesAsync(seriesId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background series enrichment failed for {SeriesId}", seriesId);
            }
            finally
            {
                _seriesEnrichmentThrottle.Release();
            }
        });
    }

    /// <summary>
    /// Cleans a raw series name extracted from a filename by replacing separators and collapsing whitespace.
    /// </summary>
    private static string CleanSeriesName(string raw)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            raw.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim(), @"\s+", " ");
    }
}
