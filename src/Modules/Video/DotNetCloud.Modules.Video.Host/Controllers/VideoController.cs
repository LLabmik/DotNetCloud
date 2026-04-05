using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Data.Services;
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
    private readonly WatchProgressService _watchProgressService;
    private readonly VideoStreamingService _streamingService;
    private readonly VideoMetadataService _metadataService;
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
        ILogger<VideoController> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _subtitleService = subtitleService;
        _watchProgressService = watchProgressService;
        _streamingService = streamingService;
        _metadataService = metadataService;
        _logger = logger;
    }

    // ─── Videos ───────────────────────────────────────────────────────

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
}
