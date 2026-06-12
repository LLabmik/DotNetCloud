using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Host.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

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
    private readonly WatchProgressService _watchProgressService;
    private readonly VideoStreamingService _streamingService;
    private readonly VideoMetadataService _metadataService;
    private readonly IDownloadService _downloadService;
    private readonly IVideoThumbnailService _thumbnailService;
    private readonly IVideoEnrichmentService _enrichmentService;
    private readonly IVideoTranscodingService _transcodingService;
    private readonly StreamProgressState _streamProgress;
    private readonly ILogger<VideoController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoController"/> class.
    /// </summary>
    public VideoController(
        VideoService videoService,
        VideoCollectionService collectionService,
        SubtitleService subtitleService,
        WatchProgressService watchProgressService,
        VideoStreamingService streamingService,
        VideoMetadataService metadataService,
        IDownloadService downloadService,
        IVideoThumbnailService thumbnailService,
        IVideoEnrichmentService enrichmentService,
        IVideoTranscodingService transcodingService,
        StreamProgressState streamProgress,
        ILogger<VideoController> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _subtitleService = subtitleService;
        _watchProgressService = watchProgressService;
        _streamingService = streamingService;
        _metadataService = metadataService;
        _downloadService = downloadService;
        _thumbnailService = thumbnailService;
        _enrichmentService = enrichmentService;
        _transcodingService = transcodingService;
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

    // ─── Watch Progress ───────────────────────────────────────────────

    /// <summary>Gets the watch progress for a specific video.</summary>
    [HttpGet("{videoId:guid}/progress")]
    public async Task<IActionResult> GetWatchProgress(Guid videoId)
    {
        var caller = GetAuthenticatedCaller();
        var progress = await _watchProgressService.GetProgressAsync(videoId, caller);
        return progress is null
            ? NotFound(ErrorEnvelope(ErrorCodes.VideoNotFound, "No progress found."))
            : Ok(Envelope(progress));
    }

    /// <summary>Updates the watch progress for a video.</summary>
    [HttpPut("{videoId:guid}/progress")]
    public async Task<IActionResult> UpdateWatchProgress(Guid videoId, [FromBody] UpdateWatchProgressDto dto)
    {
        var caller = GetAuthenticatedCaller();
        await _watchProgressService.UpdateProgressAsync(videoId, dto, caller);
        return Ok(Envelope(new { updated = true }));
    }

    /// <summary>Gets "continue watching" videos.</summary>
    [HttpGet("continue-watching")]
    public async Task<IActionResult> GetContinueWatching([FromQuery] int take = 20)
    {
        var caller = GetAuthenticatedCaller();
        var progress = await _watchProgressService.GetContinueWatchingAsync(caller, take);
        return Ok(Envelope(progress));
    }

    /// <summary>Records a view for a video.</summary>
    [HttpPost("{videoId:guid}/view")]
    public async Task<IActionResult> RecordView(Guid videoId, [FromQuery] int durationSeconds = 0)
    {
        var caller = GetAuthenticatedCaller();
        await _watchProgressService.RecordViewAsync(videoId, caller, durationSeconds);
        return Ok(Envelope(new { recorded = true }));
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
            var metadata = new DotNetCloud.Modules.Video.Models.VideoMetadata
            {
                VideoId = videoId,
                Width = dto.Width,
                Height = dto.Height,
                FrameRate = dto.FrameRate,
                VideoCodec = dto.VideoCodec,
                AudioCodec = dto.AudioCodec,
                Bitrate = dto.Bitrate,
                AudioTrackCount = dto.AudioTrackCount,
                SubtitleTrackCount = dto.SubtitleTrackCount,
                ContainerFormat = dto.ContainerFormat
            };
            await _metadataService.SaveMetadataAsync(videoId, metadata);
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
            var (strategy, videoCodec, audioCodec, container) = await _transcodingService.DecideStreamingStrategyAsync(filePath, video.MimeType);
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
        [FromQuery] bool forceTranscode = false)
    {
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

        // Reconstruct file from chunks via the Files download service.
        Stream fileStream;
        try
        {
            // ── Report reconstruction progress ──────────────────────
            var progress = _streamProgress.GetOrCreate(videoId);
            progress.Stage = StreamProgressStage.Reconstructing;
            progress.Message = "Assembling video file…";
            progress.Percent = 0;
            progress.LastUpdated = DateTime.UtcNow;

            fileStream = await _downloadService.DownloadCurrentAsync(video.FileNodeId, caller);

            // Estimate total size from the canonical video record
            var totalBytes = video.CanonicalVideo?.SizeBytes > 0
                ? video.CanonicalVideo.SizeBytes
                : fileStream.CanSeek ? fileStream.Length : 0;

            if (totalBytes > 0)
            {
                // Create a progress-reporting wrapper around the download stream
                fileStream = new ProgressReportingStream(fileStream, videoId, totalBytes, _streamProgress, _logger);
            }
            else
            {
                progress.Stage = StreamProgressStage.Reconstructing;
                progress.Message = "Assembling video file… (unknown size)";
                progress.Percent = 50; // indeterminate
            }
        }
        catch (Exception ex)
        {
            _streamProgress.Remove(videoId);
            _logger.LogWarning(ex, "Failed to reconstruct video file for {VideoId} (FileNodeId={FileNodeId})", videoId, video.FileNodeId);
            return NotFound(ErrorEnvelope("file_not_found", "Video file not found in storage."));
        }

        // Save to a persistent temp path so ffprobe and ffmpeg can access it
        var tempSourceDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-stream-source");
        Directory.CreateDirectory(tempSourceDir);

        // Background cleanup of temp files older than 24 hours
        _ = Task.Run(() => CleanupOldTempFiles(tempSourceDir, TimeSpan.FromHours(24)));

        var sourcePath = Path.Combine(tempSourceDir, $"source-{videoId:N}");

        try
        {
            // Write the temp file from the download stream (unless it already is a FileStream)
            if (fileStream is FileStream existingFs)
            {
                sourcePath = existingFs.Name;
            }
            else
            {
                await using var sourceStream = new FileStream(
                    sourcePath, FileMode.Create, FileAccess.Write,
                    FileShare.Read, 65536, FileOptions.Asynchronous);
                await fileStream.CopyToAsync(sourceStream);
            }

            // Determine MIME type for context
            var mimeType = video.CanonicalVideo?.MimeType ?? "application/octet-stream";
            _logger.LogInformation("StreamVideo: videoId={VideoId}, mimeType={MimeType}, forceTranscode={Force}",
                videoId, mimeType, forceTranscode);
            Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} mimeType={mimeType} forceTranscode={forceTranscode} sourcePath={sourcePath}");

            // ── Decide streaming strategy ──────────────────────────
            var (strategy, videoCodec, audioCodec, container) = forceTranscode
                ? (StreamingStrategy.Transcode, null, null, null)
                : await _transcodingService.DecideStreamingStrategyAsync(sourcePath, mimeType, HttpContext.RequestAborted);

            // Update progress with strategy info
            var progress = _streamProgress.GetOrCreate(videoId);
            progress.Strategy = strategy.ToString();

            _logger.LogInformation("StreamVideo: videoId={VideoId}, strategy={Strategy}, vcodec={VCodec}, acodec={ACodec}, container={Container}",
                videoId, strategy, videoCodec, audioCodec, container);
            Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} decided={strategy} vcodec={videoCodec} acodec={audioCodec} container={container}");

            // ── Strategy: Direct Play ──────────────────────────────
            if (strategy == StreamingStrategy.DirectPlay)
            {
                _logger.LogInformation("StreamVideo: Direct play for video {VideoId}", videoId);
                Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} → DIRECT_PLAY sourcePath={sourcePath}");
                var contentType = VideoStreamingService.GetContentType(mimeType);

                progress.Stage = StreamProgressStage.Streaming;
                progress.Message = "Starting playback…";
                progress.Percent = 100;

                HttpContext.Response.OnStarting(() =>
                {
                    HttpContext.Response.Headers.Remove("X-Content-Type-Options");
                    HttpContext.Response.Headers["X-Stream-Strategy"] = "direct";
                    return Task.CompletedTask;
                });

                HttpContext.Response.OnCompleted(async () =>
                {
                    _streamProgress.Remove(videoId);
                    if (fileStream is FileStream fs)
                        await fs.DisposeAsync();
                });

                return PhysicalFile(sourcePath, contentType, enableRangeProcessing: true);
            }

            // ── Strategy: Stream Copy (Remux) ─────────────────────
            if (strategy == StreamingStrategy.StreamCopy)
            {
                _logger.LogInformation("StreamVideo: Stream copy (remux) for video {VideoId}, vcodec={VCodec}, acodec={ACodec}",
                    videoId, videoCodec, audioCodec);
                Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} → STREAM_COPY/REMUX vcodec={videoCodec} acodec={audioCodec}");

                progress.Stage = StreamProgressStage.Remuxing;
                progress.Message = "Preparing stream (remux)…";
                progress.Percent = 80;

                // Stream copy to a temp file — ffmpeg remuxes near-instantly
                var remuxPath = await _transcodingService.StreamCopyToFileAsync(
                    sourcePath, videoCodec, audioCodec, HttpContext.RequestAborted);

                progress.Stage = StreamProgressStage.Streaming;
                progress.Message = "Starting playback…";
                progress.Percent = 100;

                HttpContext.Response.OnStarting(() =>
                {
                    HttpContext.Response.Headers.Remove("X-Content-Type-Options");
                    HttpContext.Response.Headers["X-Stream-Strategy"] = "remux";
                    return Task.CompletedTask;
                });

                // Clean up the remuxed file after the response completes
                HttpContext.Response.OnCompleted(() =>
                {
                    _streamProgress.Remove(videoId);
                    TryDeleteTempFile(remuxPath);
                    return Task.CompletedTask;
                });

                return PhysicalFile(remuxPath, "video/mp4", enableRangeProcessing: true);
            }

            // ── Strategy: Transcode (HLS) ──────────────────────────
            _logger.LogInformation("StreamVideo: Starting HLS transcode for video {VideoId}", videoId);
            Console.Error.WriteLine($"[VIDEO-STREAM] videoId={videoId} → TRANSCODE/HLS (WARNING: unexpected for MOV+H.264+AAC)");

            progress.Stage = StreamProgressStage.Transcoding;
            progress.Message = "Preparing stream (transcoding)…";
            progress.Percent = 90;

            var (jobId, outputDir, playlistPath) = await _transcodingService.TranscodeHlsAsync(
                videoId, userId, sourcePath, mimeType,
                sourceVideoCodec: videoCodec,
                sourceAudioCodec: audioCodec,
                ct: HttpContext.RequestAborted);

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
                _streamProgress.Remove(videoId);
                _transcodingService.CancelTranscode(jobId);
                return StatusCode(499, ErrorEnvelope("cancelled", "Client disconnected."));
            }

            if (waitResult == HlsWaitResult.Failed)
            {
                _streamProgress.Remove(videoId);
                var checkJob = _transcodingService.GetProgress(jobId);
                _logger.LogError("HLS transcode job {JobId} failed: {Error}", jobId, checkJob?.ErrorMessage);
                return StatusCode(500, ErrorEnvelope("TRANSCODE_FAILED",
                    checkJob?.ErrorMessage ?? "HLS transcoding failed."));
            }

            if (waitResult == HlsWaitResult.Timeout)
            {
                _streamProgress.Remove(videoId);
                _transcodingService.CancelTranscode(jobId);
                _logger.LogError("HLS transcode job {JobId} timed out waiting for segments", jobId);
                return StatusCode(504, ErrorEnvelope("TRANSCODE_TIMEOUT",
                    "HLS transcode did not produce segments within 30 seconds."));
            }

            progress.Stage = StreamProgressStage.Streaming;
            progress.Message = "Starting playback…";
            progress.Percent = 100;

            HttpContext.Response.OnCompleted(() =>
            {
                _streamProgress.Remove(videoId);
                return Task.CompletedTask;
            });

            _logger.LogInformation("StreamVideo: HLS playlist ready for {VideoId}, serving {PlaylistPath}",
                videoId, playlistPath);

            // Serve the .m3u8 playlist — browser/HLS player will request segments
            return PhysicalFile(playlistPath, "application/vnd.apple.mpegurl");
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
            _logger.LogWarning("HLS segment path traversal attempt: {Path}", segmentPath);
            return BadRequest(ErrorEnvelope("invalid_segment", "Invalid segment filename."));
        }

        if (!System.IO.File.Exists(fullSegmentPath))
        {
            _logger.LogDebug("HLS segment not yet available: {Path}", fullSegmentPath);
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
    /// Deletes files in the given directory older than the specified age.
    /// Best-effort — failures are silently ignored.
    /// </summary>
    private static void CleanupOldTempFiles(string directory, TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(directory))
                return;

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
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var timeoutCtr = timeoutCts.Token.Register(() => tcs.TrySetResult(HlsWaitResult.Timeout));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        // Background poller: checks job status (ffmpeg may fail silently)
        _ = Task.Run(async () =>
        {
            try
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    await Task.Delay(500, linkedCts.Token);
                    var job = _transcodingService.GetProgress(jobId);
                    if (job?.Status == TranscodingJobStatus.Failed)
                        tcs.TrySetResult(HlsWaitResult.Failed);
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
