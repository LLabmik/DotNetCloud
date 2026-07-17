using System.Diagnostics;
using System.Linq;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Host.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Video.Host.Controllers;

/// <summary>
/// REST API controller for video library management.
/// </summary>
[Route("api/v1/videos")]
public class VideoController : VideoControllerBase
{
    private readonly VideoService _videoService;
    private readonly VideoCollectionService _collectionService;
    private readonly SubtitleService _subtitleService;
    private readonly VideoStreamingService _streamingService;
    private readonly VideoMetadataService _metadataService;
    private readonly IDownloadService _downloadService;
    private readonly IVideoThumbnailService _thumbnailService;
    private readonly IVideoEnrichmentService _enrichmentService;
    private readonly IVideoTranscodingService _transcodingService;
    private readonly IWatchProgressService _watchProgressService;
    private readonly StreamProgressState _streamProgress;
    private readonly ILogger<VideoController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoController"/> class.
    /// </summary>
    public VideoController(
        VideoService videoService,
        VideoCollectionService collectionService,
        SubtitleService subtitleService,
        VideoStreamingService streamingService,
        VideoMetadataService metadataService,
        IDownloadService downloadService,
        IVideoThumbnailService thumbnailService,
        IVideoEnrichmentService enrichmentService,
        IVideoTranscodingService transcodingService,
        IWatchProgressService watchProgressService,
        StreamProgressState streamProgress,
        ILogger<VideoController> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _subtitleService = subtitleService;
        _streamingService = streamingService;
        _metadataService = metadataService;
        _downloadService = downloadService;
        _thumbnailService = thumbnailService;
        _enrichmentService = enrichmentService;
        _transcodingService = transcodingService;
        _watchProgressService = watchProgressService;
        _streamProgress = streamProgress;
        _logger = logger;
    }

    // ─── Videos ───────────────────────────────────────────────────────

    /// <summary>Serves video-player.js directly from disk,
    /// bypassing the static web assets middleware which has a bug on .NET 10.</summary>
    [AllowAnonymous]
    [HttpGet("video-player-js")]
    public IActionResult GetVideoPlayerJs()
    {
        // The file is at wwwroot/_content/DotNetCloud.Modules.Video/video-player.js
        // relative to the module host's working directory
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot", "_content", "DotNetCloud.Modules.Video", "video-player.js");

        if (!System.IO.File.Exists(path))
        {
            // Fallback: try the root wwwroot
            path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "video-player.js");
        }

        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(path, "application/javascript");
    }

    /// <summary>Lists videos in the library.</summary>
    [HttpGet]
    public async Task<IActionResult> ListVideos([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _videoService.ListVideosAsync(caller, skip, take);
        return Ok(Envelope(videos));
    }

    /// <summary>Gets a video by ID.</summary>
    [HttpGet("{videoId:guid}")]
    public async Task<IActionResult> GetVideo(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var video = await _videoService.GetVideoAsync(videoId, caller);
        return video is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."))
            : Ok(Envelope(video));
    }

    /// <summary>Triggers TMDB enrichment for a video.</summary>
    [HttpPost("{videoId:guid}/enrich")]
    public async Task<IActionResult> EnrichVideo(Guid videoId, [FromQuery] bool force = false)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _enrichmentService.EnrichVideoAsync(videoId, caller, force);
            var video = await _videoService.GetVideoAsync(videoId, caller);
            return video is null
                ? NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."))
                : Ok(Envelope(video));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
    }

    /// <summary>Gets a specific screenshot frame for a video (0-based index).</summary>
    [HttpGet("{videoId:guid}/screenshots/{index:int}")]
    public async Task<IActionResult> GetScreenshot(Guid videoId, int index)
    {
        var paths = await _thumbnailService.GetScreenshotPathsAsync(videoId);
        if (paths is null || index < 0 || index >= paths.Count)
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return PhysicalFile(paths[index], "image/jpeg");
    }

    /// <summary>Searches videos by title.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchVideos([FromQuery] string q, [FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _videoService.SearchAsync(caller, q, take);
        return Ok(Envelope(videos));
    }

    /// <summary>Gets recently added videos.</summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentVideos([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _videoService.GetRecentVideosAsync(caller, take);
        return Ok(Envelope(videos));
    }

    /// <summary>Gets favorite videos.</summary>
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavoriteVideos()
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _videoService.GetFavoritesAsync(caller);
        return Ok(Envelope(videos));
    }

    /// <summary>Toggles a video as favorite.</summary>
    [HttpPost("{videoId:guid}/favorite")]
    public async Task<IActionResult> ToggleFavorite(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var isFavorite = await _videoService.ToggleFavoriteAsync(videoId, caller);
            return Ok(Envelope(new { isFavorite }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
    }

    // ─── Watch Progress ──────────────────────────────────────────────

    /// <summary>Gets the current watch progress for a video (for resume playback).</summary>
    [HttpGet("{videoId:guid}/progress")]
    public async Task<IActionResult> GetWatchProgress(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var progress = await _watchProgressService.GetProgressAsync(videoId, caller);
        return progress is null
            ? Ok(Envelope(new { hasProgress = false }))
            : Ok(Envelope(progress));
    }

    /// <summary>Updates watch progress for a video (called periodically during playback).</summary>
    [HttpPut("{videoId:guid}/progress")]
    public async Task<IActionResult> UpdateWatchProgress(Guid videoId, [FromBody] UpdateWatchProgressDto dto)
    {
        var caller = GetAuthenticatedCaller();
        await _watchProgressService.UpdateProgressAsync(videoId, dto, caller);
        return Ok(Envelope(new { saved = true }));
    }

    /// <summary>Deletes a video (soft delete).</summary>
    [HttpDelete("{videoId:guid}")]
    public async Task<IActionResult> DeleteVideo(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _videoService.DeleteVideoAsync(videoId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
    }

    // ─── Collections ──────────────────────────────────────────────────

    /// <summary>Lists video collections for the current user.</summary>
    [HttpGet("collections")]
    public async Task<IActionResult> ListCollections()
    {
        var caller = GetAuthenticatedCaller();
        var collections = await _collectionService.ListCollectionsAsync(caller);
        return Ok(Envelope(collections));
    }

    /// <summary>Gets a video collection by ID.</summary>
    [HttpGet("collections/{collectionId:guid}")]
    public async Task<IActionResult> GetCollection(Guid collectionId)
    {
        var caller = GetAuthenticatedCaller();
        var collection = await _collectionService.GetCollectionAsync(collectionId, caller);
        return collection is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoCollectionNotFound, "Collection not found."))
            : Ok(Envelope(collection));
    }

    /// <summary>Creates a new video collection.</summary>
    [HttpPost("collections")]
    public async Task<IActionResult> CreateCollection([FromBody] CreateVideoCollectionDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var collection = await _collectionService.CreateCollectionAsync(dto, caller);
        return Created($"/api/v1/videos/collections/{collection.Id}", Envelope(collection));
    }

    /// <summary>Updates a video collection.</summary>
    [HttpPut("collections/{collectionId:guid}")]
    public async Task<IActionResult> UpdateCollection(Guid collectionId, [FromBody] UpdateVideoCollectionDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var collection = await _collectionService.UpdateCollectionAsync(collectionId, dto, caller);
            return Ok(Envelope(collection));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoCollectionNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoCollectionNotFound, ex.Message));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoAccessDenied)
        {
            return StatusCode(403, ErrorEnvelope(ErrorCodes.VideoAccessDenied, ex.Message));
        }
    }

    /// <summary>Deletes a video collection (soft delete).</summary>
    [HttpDelete("collections/{collectionId:guid}")]
    public async Task<IActionResult> DeleteCollection(Guid collectionId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _collectionService.DeleteCollectionAsync(collectionId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoCollectionNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoCollectionNotFound, ex.Message));
        }
    }

    /// <summary>Gets videos in a collection.</summary>
    [HttpGet("collections/{collectionId:guid}/videos")]
    public async Task<IActionResult> GetCollectionVideos(Guid collectionId)
    {
        var caller = GetAuthenticatedCaller();
        var videos = await _collectionService.GetCollectionVideosAsync(collectionId, caller);
        return Ok(Envelope(videos));
    }

    /// <summary>Adds a video to a collection.</summary>
    [HttpPost("collections/{collectionId:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> AddVideoToCollection(Guid collectionId, Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _collectionService.AddVideoAsync(collectionId, videoId, caller);
            return Ok(Envelope(new { added = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoAlreadyInCollection)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.VideoAlreadyInCollection, ex.Message));
        }
    }

    /// <summary>Removes a video from a collection.</summary>
    [HttpDelete("collections/{collectionId:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> RemoveVideoFromCollection(Guid collectionId, Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        await _collectionService.RemoveVideoAsync(collectionId, videoId, caller);
        return Ok(Envelope(new { removed = true }));
    }

    // ─── Subtitles ────────────────────────────────────────────────────

    /// <summary>Gets subtitles for a video.</summary>
    [HttpGet("{videoId:guid}/subtitles")]
    public async Task<IActionResult> GetSubtitles(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var subtitles = await _subtitleService.GetSubtitlesAsync(videoId, caller);
        return Ok(Envelope(subtitles));
    }

    /// <summary>Uploads a subtitle file for a video.</summary>
    [HttpPost("{videoId:guid}/subtitles")]
    public async Task<IActionResult> UploadSubtitle(Guid videoId, [FromBody] UploadSubtitleDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var subtitle = await _subtitleService.UploadSubtitleAsync(videoId, dto, caller);
            return Created($"/api/v1/videos/{videoId}/subtitles/{subtitle.Id}", Envelope(subtitle));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.InvalidSubtitleFormat)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidSubtitleFormat, ex.Message));
        }
    }

    /// <summary>Gets the content of a subtitle file.</summary>
    [HttpGet("{videoId:guid}/subtitles/{subtitleId:guid}/content")]
    public async Task<IActionResult> GetSubtitleContent(Guid videoId, Guid subtitleId)
    {
        var result = await _subtitleService.GetSubtitleContentAsync(subtitleId);
        if (result is null)
            return NotFound(ErrorEnvelope(ErrorCodes.SubtitleNotFound, "Subtitle not found."));

        var contentType = result.Value.Format == "vtt" ? "text/vtt" : "text/plain";
        return Content(result.Value.Content, contentType);
    }

    /// <summary>Deletes a subtitle.</summary>
    [HttpDelete("{videoId:guid}/subtitles/{subtitleId:guid}")]
    public async Task<IActionResult> DeleteSubtitle(Guid videoId, Guid subtitleId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _subtitleService.DeleteSubtitleAsync(subtitleId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.SubtitleNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SubtitleNotFound, ex.Message));
        }
    }

    // ─── Metadata ─────────────────────────────────────────────────────

    /// <summary>Gets metadata for a video.</summary>
    [HttpGet("{videoId:guid}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid videoId)
    {
        var metadata = await _metadataService.GetMetadataAsync(videoId);
        return metadata is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Metadata not found."))
            : Ok(Envelope(metadata));
    }

    /// <summary>Saves metadata for a video.</summary>
    [HttpPut("{videoId:guid}/metadata")]
    public async Task<IActionResult> SaveMetadata(Guid videoId, [FromBody] VideoMetadataDto dto)
    {
        try
        {
            await _metadataService.SaveMetadataAsync(videoId, dto);
            return Ok(Envelope(new { saved = true }));
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == ErrorCodes.VideoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, ex.Message));
        }
    }

    // ─── Streaming ────────────────────────────────────────────────────

    /// <summary>Generates a stream token for a video.</summary>
    [HttpPost("{videoId:guid}/stream/token")]
    public async Task<IActionResult> GetStreamToken(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var video = await _streamingService.GetVideoForStreamingAsync(videoId, caller.UserId);
        if (video is null)
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

        var token = _streamingService.GenerateStreamToken(videoId, caller.UserId);
        return Ok(Envelope(new { token, expiresInMinutes = _streamingService.StreamTokenLifetime.TotalMinutes }));
    }

    /// <summary>Gets the number of active streams for the current user.</summary>
    [HttpGet("stream/active")]
    public IActionResult GetActiveStreams()
    {
        var caller = GetAuthenticatedCaller();
        var count = _streamingService.GetActiveStreamCount(caller.UserId);
        return Ok(Envelope(new { activeStreams = count, maxStreams = _streamingService.MaxConcurrentStreams }));
    }

    /// <summary>Polls the current progress of stream preparation for a video.
    /// The frontend calls this every 500ms while showing a loading overlay.
    /// Returns the pipeline stage, percent complete, and a human-readable message.
    /// Placed BEFORE stream-level routes to avoid catch-all (*filename) conflicts.</summary>
    [AllowAnonymous]
    [HttpGet("{videoId:guid}/stream-progress")]
    public IActionResult GetStreamProgress(Guid videoId)
    {
        // Periodically clean up stale entries (entries > 5 minutes old).
        // These accumulate when we intentionally skip removal on Response.OnCompleted
        // so the JS polling can still find them after a fast pipeline completes.
        _streamProgress.RemoveStaleEntries(TimeSpan.FromMinutes(5));

        var entry = _streamProgress.Get(videoId);
        if (entry is null)
        {
            // No progress entry — stream hasn't been requested yet or already started
            return Ok(Envelope(new
            {
                stage = "unknown",
                percent = 0.0,
                message = "Waiting for stream request…"
            }));
        }

        return Ok(Envelope(new
        {
            stage = entry.Stage.ToString().ToLowerInvariant(),
            percent = entry.Percent,
            message = entry.Message,
            strategy = entry.Strategy
        }));
    }

    /// <summary>
    /// Seeks an active HLS transcode to a new position.
    /// Cancels the current transcode, cleans up old segments, and starts
    /// a new transcode from the requested position. The client should
    /// reload the HLS stream after this returns successfully.
    /// </summary>
    [HttpPost("{videoId:guid}/stream/seek")]
    public async Task<IActionResult> SeekTranscode(
        Guid videoId,
        [FromBody] SeekTranscodeDto dto)
    {
        var caller = GetAuthenticatedCaller();

        // Validate position
        if (dto.PositionSeconds < 0)
            return BadRequest(ErrorEnvelope("invalid_position", "Position must be non-negative."));

        var seekStart = TimeSpan.FromSeconds(dto.PositionSeconds);

        // Look up the video to get the file path
        var video = await _videoService.GetVideoAsync(videoId, caller);
        if (video is null)
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

        // Cancel any existing transcode for this video+user
        _transcodingService.CancelTranscode(videoId, caller.UserId);

        // Clean up old HLS output directory if it exists
        var hlsRootDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-hls");
        var oldDirPattern = $"hls-{videoId:N}-{caller.UserId:N}";
        if (Directory.Exists(hlsRootDir))
        {
            foreach (var dir in Directory.GetDirectories(hlsRootDir, oldDirPattern + "*"))
            {
                try
                { Directory.Delete(dir, recursive: true); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean old HLS dir for seek: {Dir}", dir);
                }
            }
        }

        // Reconstruct file path from storage
        var (filePath, _) = await SaveVideoToTempFile(video, caller);
        if (filePath is null)
            return NotFound(ErrorEnvelope("file_not_found", "Video file not found in storage."));

        try
        {
            // Start new HLS transcode from the seek position
            var (jobId, outputDir, playlistPath) = await _transcodingService.TranscodeHlsAsync(
                videoId,
                caller.UserId,
                filePath,
                video.MimeType,
                seekStart: seekStart,
                ct: HttpContext.RequestAborted);

            _logger.LogInformation(
                "SeekTranscode: Started new transcode job {JobId} for video {VideoId} at position {Position}s",
                jobId, videoId, dto.PositionSeconds);

            // Wait for the playlist + at least 2 segments to be ready
            var waitResult = await WaitForHlsReadyAsync(
                playlistPath, outputDir, jobId, HttpContext.RequestAborted);

            if (waitResult == HlsWaitResult.Ready)
            {
                return Ok(Envelope(new { ready = true, jobId }));
            }

            return StatusCode(504, ErrorEnvelope("TRANSCODE_TIMEOUT",
                "HLS transcode did not produce segments within 30 seconds."));
        }
        finally
        {
            TryDeleteTempFile(filePath);
        }
    }

    /// <summary>Probes a video to determine if it can be direct-played or needs transcoding.</summary>
    [HttpGet("{videoId:guid}/stream-probe")]
    public async Task<IActionResult> ProbeStream(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var video = await _videoService.GetVideoAsync(videoId, caller);
        if (video is null)
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

        // We need the actual file path — reconstruct the video to a temp file for probing
        var (filePath, _) = await SaveVideoToTempFile(video, caller);
        if (filePath is null)
            return NotFound(ErrorEnvelope("file_not_found", "Video file not found in storage."));

        try
        {
            var (strategy, videoCodec, audioCodec, container, _) = await _transcodingService.DecideStreamingStrategyAsync(filePath, video.MimeType);
            var token = _streamingService.GenerateStreamToken(videoId, caller.UserId);

            return Ok(Envelope(new
            {
                videoId = video.Id,
                canDirectPlay = strategy == StreamingStrategy.DirectPlay,
                strategy = strategy.ToString(),
                videoCodec,
                audioCodec,
                container,
                mimeType = video.MimeType,
                streamUrl = $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}" +
                            (strategy == StreamingStrategy.DirectPlay ? "" : "&forceTranscode=true")
            }));
        }
        finally
        {
            TryDeleteTempFile(filePath);
        }
    }

    /// <summary>Streams a video file.
    /// Automatically probes the file and transcodes to H.264/AAC/MP4 when direct playback
    /// is not possible (e.g. AVI, MKV, MPEG2, unsupported codecs).
    /// Use forceTranscode=true to skip the probe and always transcode.
    ///
    /// Authentication: accepts either a stream token (query param) or the session cookie.
    /// Cookie auth is used by the Blazor UI when the user is already logged in.</summary>
    [AllowAnonymous]
    [HttpGet("{videoId:guid}/stream")]
    public async Task<IActionResult> StreamVideo(
        Guid videoId,
        [FromQuery] string? token,
        [FromQuery] bool forceTranscode = false,
        [FromQuery] double? startSeconds = null)
    {
        // ── Log request headers for range request diagnostics ────────
        var rangeHeader = HttpContext.Request.Headers.Range.FirstOrDefault() ?? "(none)";
        _logger.LogInformation(
            "StreamVideo: Request for video {VideoId}, Range={Range}, token={HasToken}",
            videoId, rangeHeader, !string.IsNullOrWhiteSpace(token));
        Console.Error.WriteLine(
            $"[VIDEO-STREAM] REQUEST videoId={videoId} Range='{rangeHeader}' token={!string.IsNullOrWhiteSpace(token)}");

        Guid userId;

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Token-based auth (used by external players, mobile clients)
            var streamToken = _streamingService.ValidateStreamToken(token);
            if (streamToken is null)
                return Unauthorized(ErrorEnvelope("invalid_token", "Stream token is invalid or expired."));

            if (streamToken.VideoId != videoId)
                return Forbid();

            userId = streamToken.UserId;
        }
        else
        {
            // Cookie-based auth (used by Blazor UI — already logged in)
            try
            {
                var authCaller = GetAuthenticatedCaller();
                userId = authCaller.UserId;
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(ErrorEnvelope("auth_required",
                    "Authentication is required. Provide a stream token or log in."));
            }
        }

        // Look up the video
        var video = await _streamingService.GetVideoForStreamingAsync(videoId, userId);
        if (video is null)
            return NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "Video not found."));

        // Build caller context from the validated identity for the download service
        var caller = new CallerContext(userId, Array.Empty<string>(), CallerType.User);

        // ── File Acquisition ────────────────────────────────────────
        // Reconstruct file from chunks via the Files download service.
        // For admin share files (direct FileStream), no chunk reconstruction is needed.
        Stream fileStream;
        long totalBytes = 0;
        string? directFilePath = null;
        var pipelineStart = Stopwatch.GetTimestamp();
        try
        {
            // ── Report reconstruction progress ──────────────────────
            var progress = _streamProgress.GetOrCreate(videoId);
            progress.Stage = StreamProgressStage.Reconstructing;
            progress.Message = "Assembling video file…";
            progress.Percent = 0;
            progress.LastUpdated = DateTime.UtcNow;

            fileStream = await _downloadService.DownloadCurrentAsync(video.FileNodeId, caller);

            // ═══ CHECK FOR DIRECT FILE STREAM (admin shares) ═══
            // If the download returned a direct FileStream (e.g., admin shared folder),
            // we can serve it directly without any temp file copy.
            // IMPORTANT: Must check BEFORE wrapping with ProgressReportingStream,
            // otherwise the 'is FileStream' check will fail.
            if (fileStream is FileStream existingFs)
            {
                directFilePath = existingFs.Name;
                totalBytes = new System.IO.FileInfo(directFilePath).Length;
                progress.Stage = StreamProgressStage.Probing;
                progress.Message = "Analyzing video…";
                progress.Percent = 50;
                progress.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation(
                    "StreamVideo: Direct file stream for video {VideoId}, path={Path}, size={Size}MB",
                    videoId, directFilePath, totalBytes / (1024.0 * 1024.0));
            }
            else
            {
                // Estimate total size from the canonical video record
                totalBytes = video.CanonicalVideo?.SizeBytes > 0
                    ? video.CanonicalVideo.SizeBytes
                    : fileStream.CanSeek ? fileStream.Length : 0;

                if (totalBytes > 0)
                {
                    // Create a progress-reporting wrapper around the download stream.
                    // This only wraps non-FileStream streams (chunk-reconstructed files).
                    fileStream = new ProgressReportingStream(fileStream, videoId, totalBytes, _streamProgress, _logger);
                }
                else
                {
                    progress.Stage = StreamProgressStage.Reconstructing;
                    progress.Message = "Assembling video file… (unknown size)";
                    progress.Percent = 50; // indeterminate
                }
            }
        }
        catch (Exception ex)
        {
            _streamProgress.Remove(videoId);
            _logger.LogWarning(ex, "Failed to reconstruct video file for {VideoId} (FileNodeId={FileNodeId})", videoId, video.FileNodeId);
            return NotFound(ErrorEnvelope("file_not_found", "Video file not found in storage."));
        }

        // ── Determine source path ──────────────────────────────────
        // Either the direct file path (admin share) or a temp path for chunk-reconstructed files.
        string sourcePath;

        if (directFilePath is not null)
        {
            // Admin share file — serve directly from its physical location.
            // No temp file copy needed. ffprobe can read directly from this path.
            sourcePath = directFilePath;
            _logger.LogInformation(
                "StreamVideo: Skipping temp file copy for video {VideoId} (direct file stream)",
                videoId);
        }
        else
        {
            // Chunk-reconstructed file — write to a persistent temp path
            // so ffprobe and ffmpeg can access it.
            var tempSourceDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-stream-source");
            Directory.CreateDirectory(tempSourceDir);

            // Background cleanup of temp files older than 24 hours
            _ = Task.Run(() => CleanupOldTempFiles(
                tempSourceDir,
                Path.Combine(Path.GetTempPath(), "dotnetcloud-hls"),
                TimeSpan.FromHours(24),
                _logger));

            sourcePath = Path.Combine(tempSourceDir, $"source-{videoId:N}");

            var tempWriteStopwatch = Stopwatch.StartNew();
            try
            {
                // Write the temp file from the download stream.
                //
                // IMPORTANT: never use FileMode.Create directly on `sourcePath` while a background
                // ffmpeg process might be reading it. FileMode.Create truncates the inode in-place
                // (O_TRUNC), which causes ffmpeg to hit EOF mid-encode even though it already has
                // the file open. Instead:
                //   1. If the file already exists with the expected size, reuse it (no copy needed).
                //   2. Otherwise write to a unique .tmp file, then atomically rename into place.
                //      File.Move with overwrite=true is a rename(2) on Linux — atomic, and any
                //      ffmpeg process with the old inode open is completely unaffected.

                // Reuse the cached file if it's already complete (avoids re-copying on retries).
                var existingSize = System.IO.File.Exists(sourcePath) ? new System.IO.FileInfo(sourcePath).Length : 0L;
                var needsCopy = existingSize == 0 || (totalBytes > 0 && existingSize < totalBytes);

                if (needsCopy)
                {
                    // Write to a unique temp path to avoid truncating sourcePath in-place.
                    var tmpPath = sourcePath + $".{Guid.NewGuid():N}.tmp";
                    try
                    {
                        await using var sourceStream = new FileStream(
                            tmpPath, FileMode.Create, FileAccess.Write,
                            FileShare.Read, 65536, FileOptions.Asynchronous);
                        await fileStream.CopyToAsync(sourceStream);
                    }
                    catch
                    {
                        try
                        { System.IO.File.Delete(tmpPath); }
                        catch { /* best-effort */ }
                        throw;
                    }

                    // Atomic rename: replaces the directory entry without touching the old inode.
                    System.IO.File.Move(tmpPath, sourcePath, overwrite: true);
                }

                var writeMs = tempWriteStopwatch.ElapsedMilliseconds;

                // Verify the file was written successfully before probing.
                // Prevents ffprobe from reading a partial/corrupt file and
                // falling back to an unnecessary HLS transcode.
                var writtenSize = new System.IO.FileInfo(sourcePath).Length;
                if (writtenSize == 0)
                {
                    _logger.LogError("StreamVideo: temp file is empty after write for {VideoId}", videoId);
                    Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} → ERROR: temp file is empty");
                    _streamProgress.Remove(videoId);
                    return StatusCode(500, ErrorEnvelope("file_write_failed", "Failed to write video file to temp storage."));
                }

                if (totalBytes > 0 && writtenSize < totalBytes)
                {
                    _logger.LogWarning(
                        "StreamVideo: temp file size mismatch for {VideoId}: expected {Expected}, got {Actual} (write took {WriteMs}ms)",
                        videoId, totalBytes, writtenSize, writeMs);
                }
                else
                {
                    _logger.LogInformation(
                        "StreamVideo: Temp file written for {VideoId}: size={Size}MB, took {WriteMs}ms",
                        videoId, writtenSize / (1024.0 * 1024.0), writeMs);
                }
            }
            catch (Exception ex)
            {
                _streamProgress.Remove(videoId);
                _logger.LogError(ex, "StreamVideo: Failed to write temp file for video {VideoId}", videoId);
                return StatusCode(500, ErrorEnvelope("file_write_failed", "Failed to write video file to temp storage."));
            }
        }

        try
        {

            // Determine MIME type for context
            var mimeType = video.CanonicalVideo?.MimeType ?? "application/octet-stream";
            var beforeProbe = Stopwatch.GetTimestamp();
            _logger.LogInformation("StreamVideo: videoId={VideoId}, mimeType={MimeType}, forceTranscode={Force}",
                videoId, mimeType, forceTranscode);
            Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} mimeType={mimeType} forceTranscode={forceTranscode} sourcePath={sourcePath}");

            // ── Decide streaming strategy ──────────────────────────
            StreamingStrategy strategy;
            string? videoCodec, audioCodec, container;
            TimeSpan probeDuration = TimeSpan.Zero;

            if (forceTranscode)
            {
                (strategy, videoCodec, audioCodec, container) = (StreamingStrategy.Transcode, null, null, null);
            }
            else
            {
                (strategy, videoCodec, audioCodec, container, probeDuration) =
                    await _transcodingService.DecideStreamingStrategyAsync(sourcePath, mimeType, HttpContext.RequestAborted);

                // Backfill DurationTicks if ffprobe extracted a valid duration
                if (probeDuration > TimeSpan.Zero)
                {
                    _ = _videoService.UpdateDurationAsync(videoId, probeDuration, HttpContext.RequestAborted);
                }
            }

            var probeElapsed = Stopwatch.GetElapsedTime(beforeProbe);
            var totalElapsed = Stopwatch.GetElapsedTime(pipelineStart);

            // Update progress with strategy info
            var progress = _streamProgress.GetOrCreate(videoId);
            progress.Strategy = strategy.ToString();

            _logger.LogInformation(
                "StreamVideo: videoId={VideoId}, strategy={Strategy}, vcodec={VCodec}, acodec={ACodec}, container={Container}, " +
                "probeTime={ProbeMs}ms, totalTime={TotalMs}ms",
                videoId, strategy, videoCodec, audioCodec, container,
                probeElapsed.TotalMilliseconds, totalElapsed.TotalMilliseconds);
            Console.Error.WriteLine(
                $"[VIDEO-STREAM] videoId={videoId} decided={strategy} vcodec={videoCodec} acodec={audioCodec} " +
                $"container={container} probe={probeElapsed.TotalMilliseconds:F0}ms total={totalElapsed.TotalMilliseconds:F0}ms");

            // ── Strategy: Direct Play ──────────────────────────────
            if (strategy == StreamingStrategy.DirectPlay)
            {
                _logger.LogInformation(
                    "StreamVideo: Direct play for video {VideoId} (probe={ProbeMs}ms, total={TotalMs}ms)",
                    videoId, probeElapsed.TotalMilliseconds, totalElapsed.TotalMilliseconds);
                Console.Error.WriteLine(
                    $"[VIDEO-STREAM] videoId={videoId} → DIRECT_PLAY sourcePath={sourcePath} " +
                    $"probe={probeElapsed.TotalMilliseconds:F0}ms total={totalElapsed.TotalMilliseconds:F0}ms");

                // Safeguard: if the mimeType resolved to application/octet-stream,
                // default to video/mp4 for DirectPlay-capable files. Otherwise the
                // browser will download instead of streaming.
                var contentType = VideoStreamingService.GetContentType(mimeType);
                if (contentType == "application/octet-stream")
                {
                    _logger.LogWarning(
                        "StreamVideo: Falling back to video/mp4 for video {VideoId} (original mimeType={MimeType})",
                        videoId, mimeType);
                    contentType = "video/mp4";
                }

                progress.Stage = StreamProgressStage.Streaming;
                progress.Message = "Starting playback…";
                progress.Percent = 100;

                // NOTE: Do NOT remove progress entry here. The JS showStreamProgress()
                // polls this endpoint and needs to find the entry with stage=Streaming.
                // If the pipeline completes before the first poll, the entry is gone and
                // the loading overlay stays forever. Let RemoveStaleEntries handle cleanup.

                // ═══ Manual streaming with explicit range handling ═══
                // PhysicalFile with enableRangeProcessing=true has issues with certain
                // Range header formats (e.g. duplicated "bytes=0-, bytes=0-") that
                // browsers sometimes send. We handle ranges manually to ensure reliable
                // 206 Partial Content responses.
                var fileInfo = new System.IO.FileInfo(sourcePath);
                var fileLength = fileInfo.Length;

                HttpContext.Response.StatusCode = 200;
                HttpContext.Response.ContentType = contentType;
                HttpContext.Response.Headers.AcceptRanges = "bytes";
                HttpContext.Response.Headers["X-Stream-Strategy"] = "direct";
                HttpContext.Response.Headers["X-Stream-Diagnostics"] =
                    $"strategy=direct;probe={probeElapsed.TotalMilliseconds:F0}ms;total={totalElapsed.TotalMilliseconds:F0}ms;codec={videoCodec};container={container}";

                // Parse the Range header, handling the common case where browsers send
                // "bytes=0-, bytes=0-" (two identical ranges in one header value).
                // We take only the first range spec and ignore multi-range requests.
                long rangeStart = 0;
                long rangeEnd = fileLength - 1;
                bool isRangeRequest = false;

                var rawRange = HttpContext.Request.Headers.Range.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(rawRange) &&
                    rawRange.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    // Split on ',' and take first range only (ignore duplicates/multi-ranges)
                    var rangePart = rawRange["bytes=".Length..].Split(',')[0].Trim();
                    var parts = rangePart.Split('-');

                    if (parts.Length == 2)
                    {
                        if (long.TryParse(parts[0], out var start) && start >= 0 && start < fileLength)
                        {
                            rangeStart = start;
                            rangeEnd = string.IsNullOrEmpty(parts[1])
                                ? fileLength - 1
                                : Math.Min(long.Parse(parts[1]), fileLength - 1);
                            isRangeRequest = true;
                        }
                        else if (string.IsNullOrEmpty(parts[0]) &&
                                 long.TryParse(parts[1], out var suffix) && suffix > 0)
                        {
                            // Suffix range: bytes=-N → last N bytes
                            rangeStart = Math.Max(0, fileLength - suffix);
                            rangeEnd = fileLength - 1;
                            isRangeRequest = true;
                        }
                    }
                }

                if (isRangeRequest)
                {
                    var contentLength = rangeEnd - rangeStart + 1;
                    HttpContext.Response.StatusCode = 206;
                    HttpContext.Response.Headers.ContentRange = $"bytes {rangeStart}-{rangeEnd}/{fileLength}";
                    HttpContext.Response.ContentLength = contentLength;

                    _logger.LogInformation(
                        "StreamVideo: 206 for video {VideoId}: {Start}-{End}/{Length} ({Size:F1}MB chunk)",
                        videoId, rangeStart, rangeEnd, fileLength,
                        contentLength / (1024.0 * 1024.0));
                    Console.Error.WriteLine(
                        $"[VIDEO-STREAM] videoId={videoId} → 206 range={rangeStart}-{rangeEnd}/{fileLength} chunk={contentLength / (1024.0 * 1024.0):F1}MB");
                }
                else
                {
                    HttpContext.Response.ContentLength = fileLength;
                    Console.Error.WriteLine(
                        $"[VIDEO-STREAM] videoId={videoId} → 200 full={fileLength / (1024.0 * 1024.0):F1}MB");
                }

                // Stream the file (or byte range) directly to the response body
                try
                {
                    await using var outputStream = new FileStream(
                        sourcePath, FileMode.Open, FileAccess.Read,
                        FileShare.Read, 65536,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                    if (isRangeRequest && rangeStart > 0)
                    {
                        outputStream.Seek(rangeStart, SeekOrigin.Begin);
                    }

                    var count = isRangeRequest ? rangeEnd - rangeStart + 1 : fileLength;
                    await outputStream.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted);

                    _streamProgress.Remove(videoId);
                }
                catch (OperationCanceledException)
                {
                    // Client disconnected — expected, don't log as error
                    _streamProgress.Remove(videoId);
                }
                catch (Exception ex)
                {
                    _streamProgress.Remove(videoId);
                    _logger.LogDebug(ex, "StreamVideo: Client disconnected for video {VideoId}", videoId);
                }
                finally
                {
                    // Dispose the original download service stream (admin share FileStream)
                    if (fileStream is FileStream fs)
                        fs.Dispose();
                }

                return new EmptyResult();
            }

            // ── Strategy: Stream Copy (Remux) ─────────────────────
            if (strategy == StreamingStrategy.StreamCopy)
            {
                _logger.LogInformation(
                    "StreamVideo: Stream copy (remux) for video {VideoId}, vcodec={VCodec}, acodec={ACodec} (total={TotalMs}ms)",
                    videoId, videoCodec, audioCodec, totalElapsed.TotalMilliseconds);
                Console.Error.WriteLine(
                    $"[VIDEO-STREAM] videoId={videoId} → STREAM_COPY/REMUX vcodec={videoCodec} acodec={audioCodec} " +
                    $"total={totalElapsed.TotalMilliseconds:F0}ms");

                progress.Stage = StreamProgressStage.Remuxing;
                progress.Message = "Starting stream…";
                progress.Percent = 50;

                // Start ffmpeg remux with stdout piped for progressive streaming
                var startTime = startSeconds.HasValue && startSeconds.Value > 0
                    ? TimeSpan.FromSeconds(startSeconds.Value) : (TimeSpan?)null;
                var (ffmpegProcess, _) = await _transcodingService.StreamCopyAsync(
                    sourcePath, videoCodec, audioCodec, HttpContext.RequestAborted, startTime);

                progress.Stage = StreamProgressStage.Streaming;
                progress.Message = "Streaming…";
                progress.Percent = 100;

                var response = HttpContext.Response;
                response.ContentType = "video/mp4";
                response.Headers.Remove("X-Content-Type-Options");
                response.Headers["X-Stream-Strategy"] = "remux";
                response.Headers["X-Stream-Diagnostics"] =
                    $"strategy=remux;total={totalElapsed.TotalMilliseconds:F0}ms;codec={videoCodec};container={container}";

                // Stream ffmpeg stdout directly to the HTTP response
                try
                {
                    await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(
                        response.Body, HttpContext.RequestAborted);
                }
                finally
                {
                    _streamProgress.Remove(videoId);
                    if (!ffmpegProcess.HasExited)
                    {
                        try
                        { ffmpegProcess.Kill(entireProcessTree: true); }
                        catch { /* best effort */ }
                    }
                    ffmpegProcess.Dispose();
                }

                return new EmptyResult();
            }

            // ── Strategy: Transcode (HLS) ──────────────────────────
            _logger.LogInformation(
                "StreamVideo: Starting HLS transcode for video {VideoId} (total={TotalMs}ms)",
                videoId, totalElapsed.TotalMilliseconds);
            Console.Error.WriteLine(
                $"[VIDEO-STREAM] videoId={videoId} → TRANSCODE/HLS total={totalElapsed.TotalMilliseconds:F0}ms");

            progress.Stage = StreamProgressStage.Transcoding;
            progress.Message = "Preparing stream (transcoding)…";
            progress.Percent = 90;

            // Use CancellationToken.None for the ffmpeg transcode so it survives
            // past the HTTP response. Do NOT create a CTS here — C# 'using' disposes
            // the CTS when StreamVideo returns (after sending the playlist), which
            // calls Cancel() on the token. That sends 'q' to ffmpeg's stdin via the
            // registered callback in FfmpegProcessManager.RunAsync, causing ffmpeg
            // to gracefully stop after only a few segments. ffmpeg must run to
            // completion independently of the HTTP request lifetime.
            // We still use a CTS for error-path cancellation (client disconnect
            // before playlist is ready), but it's NOT disposed here — it's captured
            // by the error handlers and allowed to be GC'd naturally.
            var transcodeCts = new CancellationTokenSource();
            var transcodeCt = transcodeCts.Token;

            var (jobId, outputDir, playlistPath) = await _transcodingService.TranscodeHlsAsync(
                videoId, userId, sourcePath, mimeType,
                sourceVideoCodec: videoCodec,
                sourceAudioCodec: audioCodec,
                ct: transcodeCt);

            _logger.LogInformation("StreamVideo: HLS transcode job {JobId} started, playlist={PlaylistPath}",
                jobId, playlistPath);

            HttpContext.Response.Headers["X-Stream-Strategy"] = "transcode";

            // Wait for the playlist file + at least 2 segments using FileSystemWatcher.
            // This is event-driven — no polling loop. ffmpeg writes the playlist after
            // the first segment is complete (~6 seconds).
            var waitResult = await WaitForHlsReadyAsync(playlistPath, outputDir, jobId,
                HttpContext.RequestAborted);

            if (waitResult == HlsWaitResult.Cancelled)
            {
                // Do NOT cancel the transcode — the background ffmpeg job keeps running.
                // The browser often disconnects immediately after receiving the response
                // headers (or during navigation), which fires RequestAborted. If we killed
                // ffmpeg here, every retry would restart from scratch. Instead, keep the
                // job running so the next request reuses it via IsJobReusable.
                _streamProgress.Remove(videoId);
                return StatusCode(499, ErrorEnvelope("cancelled", "Client disconnected."));
            }

            if (waitResult == HlsWaitResult.Failed)
            {
                transcodeCts.Cancel();
                _streamProgress.Remove(videoId);
                var checkJob = _transcodingService.GetProgress(jobId);
                _logger.LogError("HLS transcode job {JobId} failed: {Error}", jobId, checkJob?.ErrorMessage);
                return StatusCode(500, ErrorEnvelope("TRANSCODE_FAILED",
                    checkJob?.ErrorMessage ?? "HLS transcoding failed."));
            }

            if (waitResult == HlsWaitResult.Timeout)
            {
                // Do NOT cancel the transcode — ffmpeg is still producing segments.
                // On retry, the existing job is reused and segments are ready.
                _streamProgress.Remove(videoId);
                _logger.LogError("HLS transcode job {JobId} timed out waiting for segments", jobId);
                return StatusCode(504, ErrorEnvelope("TRANSCODE_TIMEOUT",
                    "HLS transcode did not produce segments within 30 seconds."));
            }

            // ffmpeg is now running and producing segments — do NOT cancel it.
            // The transcodeCts is intentionally not disposed here (no 'using') so
            // the token stays uncancelled. C# disposing a CancellationTokenSource
            // calls Cancel(), which sends 'q' to ffmpeg's stdin via the registered
            // callback in FfmpegProcessManager — causing ffmpeg to write ENDLIST
            // and exit after only a few segments. The transcodeCts will be GC'd
            // naturally when no longer referenced. ffmpeg's background task in
            // LaunchFfmpegAsync handles cleanup after completion.

            progress.Stage = StreamProgressStage.Streaming;
            progress.Message = "Starting playback…";
            progress.Percent = 100;

            // IMPORTANT: Do NOT remove the progress entry on Response.OnCompleted.
            // The JS showStreamProgress() polls this endpoint, and when the pipeline
            // completes instantly (e.g. FindExistingHlsOutput finds pre-completed HLS),
            // OnCompleted fires BEFORE the JS polling even begins. Removing the entry
            // causes the poll to always return {stage:"unknown"} — the promise never
            // resolves, playStream() is never called, and hls.js is never initialized.
            // The entry will be cleaned up when a new stream request overwrites it.
            // A background cleanup runs periodically to remove stale entries.

            _logger.LogInformation("StreamVideo: HLS playlist ready for {VideoId}, serving {PlaylistPath}",
                videoId, playlistPath);

            // Read the playlist and rewrite segment paths to use the /stream/ prefix
            // so hls.js resolves segments to the primary route:
            //   /api/v1/videos/{id}/stream/segment_00000.ts
            // instead of the fallback:
            //   /api/v1/videos/{id}/segment_00000.ts
            var playlistContent = await System.IO.File.ReadAllTextAsync(playlistPath, HttpContext.RequestAborted);
            var rewrittenPlaylist = playlistContent.Replace(
                "segment_",
                "stream/segment_");

            // Serve the .m3u8 playlist — browser/HLS player will request segments.
            // no-cache is critical: hls.js must re-fetch the playlist as new segments appear.
            HttpContext.Response.Headers["Cache-Control"] = "no-cache";
            return Content(rewrittenPlaylist, "application/vnd.apple.mpegurl");
        }
        catch (Exception ex)
        {
            _streamProgress.Remove(videoId);
            _logger.LogError(ex, "StreamVideo failed for video {VideoId}", videoId);
            Console.Error.WriteLine($"[VIDEO-STREAM-FAIL] videoId={videoId} error={ex.GetType().Name}: {ex.Message}");
            return StatusCode(500, ErrorEnvelope("TRANSCODE_ERROR",
                $"Video streaming failed: {ex.GetType().Name} — {ex.Message}"));
        }
        // NOTE: Do NOT delete sourcePath here — ffmpeg is still reading from it.
        // The transcode service cleans up the HLS directory when done.
    }

    /// <summary>Serves HLS segment (.ts) files for an active HLS transcode stream.
    /// These are requested by the browser/HLS player after receiving the .m3u8 playlist.
    /// Authentication: accepts either a stream token (query param) or the session cookie.</summary>
    [AllowAnonymous]
    [HttpGet("{videoId:guid}/stream/{*filename}")]
    public IActionResult GetHlsSegment(
        Guid videoId,
        string filename,
        [FromQuery] string? token)
    {
        if (!IsSafeHlsRelativePath(filename))
        {
            _logger.LogWarning("Invalid HLS segment filename: {Filename}", SanitizeForLog(filename));
            return BadRequest(ErrorEnvelope("invalid_segment", "Invalid segment filename."));
        }

        Guid userId;

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Token-based auth
            var streamToken = _streamingService.ValidateStreamToken(token);
            if (streamToken is null)
                return Unauthorized(ErrorEnvelope("invalid_token", "Stream token is invalid or expired."));

            if (streamToken.VideoId != videoId)
                return Forbid();

            userId = streamToken.UserId;
        }
        else
        {
            // Cookie-based auth
            try
            {
                var authCaller = GetAuthenticatedCaller();
                userId = authCaller.UserId;
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(ErrorEnvelope("auth_required",
                    "Authentication is required. Provide a stream token or log in."));
            }
        }

        // Only serve .ts/.m4s segment files and .m3u8 playlists
        var isTs = filename.EndsWith(".ts", StringComparison.OrdinalIgnoreCase);
        var isM4s = filename.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase);
        var isMp4 = filename.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        var isM3u8 = filename.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        if (!isTs && !isM4s && !isMp4 && !isM3u8)
        {
            return BadRequest(ErrorEnvelope("invalid_segment", "Only .ts, .m4s, .mp4, and .m3u8 files are supported."));
        }

        // Find the active HLS job for this video
        var job = _transcodingService.GetActiveHlsJob(videoId);
        if (job is null || string.IsNullOrEmpty(job.OutputPath))
        {
            _logger.LogWarning("HLS segment requested but no active HLS job for video {VideoId}", videoId);
            return NotFound(ErrorEnvelope("no_active_stream", "No active HLS stream for this video."));
        }

        // Derive the output directory from the playlist path
        var outputDir = Path.GetDirectoryName(job.OutputPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            return StatusCode(500, ErrorEnvelope("internal_error", "Invalid HLS job output path."));
        }

        var segmentPath = Path.Combine(outputDir, filename);

        // Normalize to prevent directory traversal
        var fullSegmentPath = Path.GetFullPath(segmentPath);
        var fullOutputDir = Path.GetFullPath(outputDir);
        if (!fullSegmentPath.StartsWith(fullOutputDir + Path.DirectorySeparatorChar) &&
            fullSegmentPath != fullOutputDir)
        {
            _logger.LogWarning("HLS segment path traversal attempt: {Path}", SanitizeForLog(segmentPath));
            return BadRequest(ErrorEnvelope("invalid_segment", "Invalid segment filename."));
        }

        if (!System.IO.File.Exists(fullSegmentPath))
        {
            _logger.LogDebug("HLS segment not yet available: {Path}", SanitizeForLog(fullSegmentPath));
            return NotFound(ErrorEnvelope("segment_not_found", "Segment not yet available."));
        }

        var contentType = isM3u8
            ? "application/vnd.apple.mpegurl"
            : isTs ? "video/mp2t"
            : "video/mp4";

        return PhysicalFile(fullSegmentPath, contentType);
    }

    /// <summary>Gets the progress of a transcode job.</summary>
    [HttpGet("transcodes/{jobId}/progress")]
    public IActionResult GetTranscodeProgress(string jobId)
    {
        var job = _transcodingService.GetProgress(jobId);
        if (job is null)
            return NotFound(ErrorEnvelope("JobNotFound", "Transcode job not found."));

        return Ok(Envelope(new
        {
            jobId = job.Id,
            status = job.Status.ToString(),
            progressPercent = job.ProgressPercent,
            currentTime = job.CurrentTime.ToString(@"hh\:mm\:ss"),
            speed = job.Speed,
            errorMessage = job.ErrorMessage
        }));
    }

    /// <summary>Cancels a running transcode job.</summary>
    [HttpDelete("transcodes/{jobId}")]
    public IActionResult CancelTranscode(string jobId)
    {
        _transcodingService.CancelTranscode(jobId);
        return Ok(Envelope(new { cancelled = true }));
    }

    /// <summary>Cancels any active stream preparation for a video (e.g. when user closes player).</summary>
    [HttpPost("cancel-stream/{videoId:guid}")]
    public IActionResult CancelStreamPrep(Guid videoId)
    {
        try
        {
            var caller = GetAuthenticatedCaller();
            _transcodingService.CancelTranscode(videoId, caller.UserId);
            _streamProgress.Remove(videoId);
            return Ok(Envelope(new { cancelled = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelStreamPrep failed for video {VideoId}", videoId);
            return StatusCode(500, ErrorEnvelope("CANCEL_FAILED", ex.Message));
        }
    }

    private static bool IsSafeHlsRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (Path.IsPathRooted(path))
            return false;

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.All(segment => segment != "..");
    }

    /// <summary>Fallback route for HLS segments requested at the video level
    /// (e.g., /api/v1/videos/{id}/segment_00000.ts). This happens because .m3u8 relative
    /// paths resolve against the playlist URL's directory, which is the video path.</summary>
    [AllowAnonymous]
    [HttpGet("{videoId:guid}/{*filename}")]
    public IActionResult GetHlsSegmentFallback(
        Guid videoId,
        string filename,
        [FromQuery] string? token)
    {
        // Only handle .ts/.m4s/.mp4/.m3u8 files; let other routes pass through
        if (!filename.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
            !filename.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase) &&
            !filename.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) &&
            !filename.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return GetHlsSegment(videoId, filename, token);
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    // ─── Private Helpers ─────────────────────────────────────────────

    private async Task<(string? FilePath, Stream? Stream)> SaveVideoToTempFile(VideoDto video, CallerContext caller)
    {
        try
        {
            var fs = await _downloadService.DownloadCurrentAsync(video.FileNodeId, caller);
            if (fs is FileStream fileStream)
                return (fileStream.Name, fileStream);
            return (null, fs);
        }
        catch
        {
            return (null, null);
        }
    }

    private static void TryDeleteTempFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Deletes files in the given directory and old HLS output directories
    /// older than the specified age. Best-effort — failures are silently ignored.
    /// </summary>
    /// <param name="directory">Temp source file directory to clean.</param>
    /// <param name="hlsRoot">HLS output root directory (may be null).</param>
    /// <param name="maxAge">Maximum age before deletion.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    private static void CleanupOldTempFiles(string directory, string? hlsRoot, TimeSpan maxAge, ILogger? logger = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var cutoff = DateTime.UtcNow - maxAge;
                foreach (var file in Directory.GetFiles(directory))
                {
                    try
                    {
                        if (System.IO.File.GetLastWriteTimeUtc(file) < cutoff)
                            System.IO.File.Delete(file);
                    }
                    catch { /* best effort per file */ }
                }
            }

            // Clean old HLS output directories (completed transcodes). These
            // accumulate over time and should be removed to free disk space.
            if (!string.IsNullOrEmpty(hlsRoot) && Directory.Exists(hlsRoot))
            {
                var cutoff = DateTime.UtcNow - maxAge;
                foreach (var dir in Directory.EnumerateDirectories(hlsRoot, "hls-*"))
                {
                    try
                    {
                        var lastWrite = Directory.GetLastWriteTimeUtc(dir);
                        if (lastWrite < cutoff)
                        {
                            Directory.Delete(dir, recursive: true);
                            logger?.LogDebug("Cleaned up old HLS directory: {Dir} (last modified {LastWrite})",
                                dir, lastWrite);
                        }
                    }
                    catch { /* best effort per directory */ }
                }
            }
        }
        catch { /* best effort */ }
    }

    // ─── HLS Wait Helper (FileSystemWatcher-based) ──────────────────

    private enum HlsWaitResult { Ready, Cancelled, Failed, Timeout }

    /// <summary>
    /// Waits for the HLS playlist and at least 2 .ts segments to appear using
    /// FileSystemWatcher (event-driven, no polling). Times out after 30 seconds.
    /// Also polls the transcode job for failure every 500ms.
    /// </summary>
    private async Task<HlsWaitResult> WaitForHlsReadyAsync(
        string playlistPath,
        string outputDir,
        string jobId,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<HlsWaitResult>();
        using var ctr = ct.Register(() => tcs.TrySetResult(HlsWaitResult.Cancelled));
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var timeoutCtr = timeoutCts.Token.Register(() => tcs.TrySetResult(HlsWaitResult.Timeout));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        // Background poller: checks job status (ffmpeg may fail silently) and segment readiness.
        // FileSystemWatcher only fires on playlist Created, which can happen before segments appear.
        // The poller catches the gap when playlist exists but < 2 segments exist yet.
        _ = Task.Run(async () =>
        {
            try
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    await Task.Delay(500, linkedCts.Token);
                    var job = _transcodingService.GetProgress(jobId);
                    if (job?.Status == TranscodingJobStatus.Failed)
                    {
                        tcs.TrySetResult(HlsWaitResult.Failed);
                        return;
                    }
                    if (HasMinSegments(outputDir))
                        tcs.TrySetResult(HlsWaitResult.Ready);
                }
            }
            catch (OperationCanceledException) { /* Expected */ }
        }, linkedCts.Token);

        // Use FileSystemWatcher to detect when playlist.m3u8 appears
        using var watcher = new FileSystemWatcher(outputDir, "playlist.m3u8")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = false
        };

        try
        {
            // If playlist already exists, check segments immediately
            if (System.IO.File.Exists(playlistPath) && HasMinSegments(outputDir))
                return HlsWaitResult.Ready;

            watcher.Created += (_, _) =>
            {
                if (HasMinSegments(outputDir))
                    tcs.TrySetResult(HlsWaitResult.Ready);
            };
            watcher.EnableRaisingEvents = true;

            return await tcs.Task;
        }
        finally
        {
            linkedCts.Cancel();
            linkedCts.Dispose();
        }
    }

    /// <summary>
    /// Returns true if the output directory contains at least 2 .ts segment files.
    /// </summary>
    private static bool HasMinSegments(string outputDir)
    {
        try
        {
            return Directory.EnumerateFiles(outputDir, "segment_*.ts").Take(2).Count() >= 2;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Request DTO for seeking an active HLS transcode to a new position.
/// </summary>
public sealed class SeekTranscodeDto
{
    /// <summary>The target position in seconds (may have decimal precision).</summary>
    public double PositionSeconds { get; set; }
}
