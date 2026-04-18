using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Photos.Host.Controllers;

/// <summary>
/// REST API controller for photo and album management.
/// </summary>
[Route("api/v1/photos")]
public class PhotosController : PhotosControllerBase
{
    private readonly PhotoService _photoService;
    private readonly AlbumService _albumService;
    private readonly PhotoMetadataService _metadataService;
    private readonly PhotoGeoService _geoService;
    private readonly PhotoShareService _shareService;
    private readonly PhotoEditService _editService;
    private readonly SlideshowService _slideshowService;
    private readonly IPhotoThumbnailService _thumbnailService;
    private readonly ILogger<PhotosController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosController"/> class.
    /// </summary>
    public PhotosController(
        PhotoService photoService,
        AlbumService albumService,
        PhotoMetadataService metadataService,
        PhotoGeoService geoService,
        PhotoShareService shareService,
        PhotoEditService editService,
        SlideshowService slideshowService,
        IPhotoThumbnailService thumbnailService,
        ILogger<PhotosController> logger)
    {
        _photoService = photoService;
        _albumService = albumService;
        _metadataService = metadataService;
        _geoService = geoService;
        _shareService = shareService;
        _editService = editService;
        _slideshowService = slideshowService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    // ─── Photo CRUD ───────────────────────────────────────────────────────

    /// <summary>Lists photos for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListPhotosAsync([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var photos = await _photoService.ListPhotosAsync(caller, skip, take);
        return Ok(Envelope(photos));
    }

    /// <summary>Gets a photo by ID.</summary>
    [HttpGet("{photoId:guid}")]
    public async Task<IActionResult> GetPhotoAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        var photo = await _photoService.GetPhotoAsync(photoId, caller);
        return photo is null
            ? NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, "Photo not found."))
            : Ok(Envelope(photo));
    }

    /// <summary>Gets a thumbnail for a photo at the requested size.</summary>
    [HttpGet("{photoId:guid}/thumbnail")]
    public async Task<IActionResult> GetThumbnailAsync(Guid photoId, [FromQuery] string size = "grid")
    {
        if (!Enum.TryParse<PhotoThumbnailSize>(size, ignoreCase: true, out var thumbnailSize))
            thumbnailSize = PhotoThumbnailSize.Grid;

        var (stream, contentType) = await _thumbnailService.GetThumbnailAsync(photoId, thumbnailSize);
        if (stream is null)
            return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return File(stream, contentType ?? "image/jpeg");
    }

    /// <summary>Creates a photo record linked to a file node.</summary>
    [HttpPost]
    public async Task<IActionResult> CreatePhotoAsync([FromBody] CreatePhotoRequest dto)
    {
        var caller = GetAuthenticatedCaller();
        var photo = await _photoService.CreatePhotoAsync(
            dto.FileNodeId, dto.FileName, dto.MimeType, dto.SizeBytes, caller.UserId, caller);
        return Created($"/api/v1/photos/{photo.Id}", Envelope(photo));
    }

    /// <summary>Deletes a photo (soft delete).</summary>
    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> DeletePhotoAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _photoService.DeletePhotoAsync(photoId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
    }

    /// <summary>Toggles the favorite flag on a photo.</summary>
    [HttpPost("{photoId:guid}/favorite")]
    public async Task<IActionResult> ToggleFavoriteAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var photo = await _photoService.ToggleFavoriteAsync(photoId, caller);
            return Ok(Envelope(photo));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
    }

    /// <summary>Gets favorite photos for the authenticated user.</summary>
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavoritesAsync()
    {
        var caller = GetAuthenticatedCaller();
        var photos = await _photoService.GetFavoritesAsync(caller);
        return Ok(Envelope(photos));
    }

    /// <summary>Searches photos by query term.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync([FromQuery] string q)
    {
        var caller = GetAuthenticatedCaller();
        var photos = await _photoService.SearchAsync(caller, q);
        return Ok(Envelope(photos));
    }

    // ─── Timeline ─────────────────────────────────────────────────────────

    /// <summary>Gets photos within a date range (timeline view).</summary>
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimelineAsync(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var caller = GetAuthenticatedCaller();
        var photos = await _photoService.GetTimelineAsync(caller, from, to);
        return Ok(Envelope(photos));
    }

    // ─── Metadata ─────────────────────────────────────────────────────────

    /// <summary>Gets EXIF metadata for a photo.</summary>
    [HttpGet("{photoId:guid}/metadata")]
    public async Task<IActionResult> GetMetadataAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        var photo = await _photoService.GetPhotoAsync(photoId, caller);
        if (photo is null)
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, "Photo not found."));

        var metadata = await _metadataService.GetMetadataAsync(photoId);
        return Ok(Envelope(metadata!));
    }

    // ─── Geo ──────────────────────────────────────────────────────────────

    /// <summary>Gets geo-tagged photos for the authenticated user.</summary>
    [HttpGet("geo")]
    public async Task<IActionResult> GetGeoTaggedAsync()
    {
        var caller = GetAuthenticatedCaller();
        var photos = await _geoService.GetGeoTaggedPhotosAsync(caller.UserId);
        return Ok(Envelope(photos));
    }

    /// <summary>Gets geo clusters for the map view.</summary>
    [HttpGet("geo/clusters")]
    public async Task<IActionResult> GetGeoClustersAsync([FromQuery] double gridSize = 1.0)
    {
        var caller = GetAuthenticatedCaller();
        var clusters = await _geoService.GetGeoClustersAsync(caller.UserId, gridSize);
        return Ok(Envelope(clusters));
    }

    // ─── Editing ──────────────────────────────────────────────────────────

    /// <summary>Gets the edit stack for a photo.</summary>
    [HttpGet("{photoId:guid}/edits")]
    public async Task<IActionResult> GetEditStackAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        var photo = await _photoService.GetPhotoAsync(photoId, caller);
        if (photo is null)
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, "Photo not found."));

        var edits = await _editService.GetEditStackAsync(photoId);
        return Ok(Envelope(edits));
    }

    /// <summary>Applies an edit operation to a photo.</summary>
    [HttpPost("{photoId:guid}/edits")]
    public async Task<IActionResult> ApplyEditAsync(Guid photoId, [FromBody] PhotoEditOperationDto operation)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var edit = await _editService.ApplyEditAsync(photoId, operation, caller);
            return Ok(Envelope(edit));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.InvalidPhotoEdit)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidPhotoEdit, ex.Message));
        }
    }

    /// <summary>Undoes the last edit on a photo.</summary>
    [HttpDelete("{photoId:guid}/edits/last")]
    public async Task<IActionResult> UndoLastEditAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _editService.UndoLastEditAsync(photoId, caller);
            return Ok(Envelope(new { undone = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
    }

    /// <summary>Reverts all edits on a photo.</summary>
    [HttpDelete("{photoId:guid}/edits")]
    public async Task<IActionResult> RevertAllEditsAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _editService.RevertAllAsync(photoId, caller);
            return Ok(Envelope(new { reverted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
    }

    /// <summary>
    /// Applies the current edit stack to the original image and regenerates thumbnails.
    /// The original file on disk is preserved (non-destructive).
    /// </summary>
    [HttpPost("{photoId:guid}/edits/save")]
    public async Task<IActionResult> SaveEditsAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();

        // Verify photo exists and belongs to caller
        var photo = await _photoService.GetPhotoAsync(photoId, caller);
        if (photo is null)
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, "Photo not found."));

        var success = await _thumbnailService.SaveEditsAsync(photoId);
        if (!success)
            return StatusCode(500, ErrorEnvelope("SAVE_EDITS_FAILED", "Failed to apply edits to thumbnails."));

        return Ok(Envelope(new { saved = true }));
    }

    // ─── Sharing ──────────────────────────────────────────────────────────

    /// <summary>Creates a share for a photo.</summary>
    [HttpPost("{photoId:guid}/shares")]
    public async Task<IActionResult> SharePhotoAsync(Guid photoId, [FromBody] CreatePhotoShareRequest dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var share = await _shareService.SharePhotoAsync(
                photoId, dto.SharedWithUserId!.Value, dto.Permission, caller);
            return Created($"/api/v1/photos/{photoId}/shares/{share.Id}", Envelope(share));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoNotFound, ex.Message));
        }
    }

    /// <summary>Gets shares for a photo.</summary>
    [HttpGet("{photoId:guid}/shares")]
    public async Task<IActionResult> GetPhotoSharesAsync(Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        var shares = await _shareService.GetPhotoSharesAsync(photoId, caller);
        return Ok(Envelope(shares));
    }

    /// <summary>Removes a share.</summary>
    [HttpDelete("shares/{shareId:guid}")]
    public async Task<IActionResult> RemoveShareAsync(Guid shareId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _shareService.RemoveShareAsync(shareId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoShareNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PhotoShareNotFound, ex.Message));
        }
    }

    /// <summary>Gets photos/albums shared with the authenticated user.</summary>
    [HttpGet("shared-with-me")]
    public async Task<IActionResult> GetSharedWithMeAsync()
    {
        var caller = GetAuthenticatedCaller();
        var shares = await _shareService.GetSharedWithMeAsync(caller);
        return Ok(Envelope(shares));
    }

    // ─── Slideshow ────────────────────────────────────────────────────────

    /// <summary>Creates a slideshow from an album.</summary>
    [HttpPost("slideshows/from-album/{albumId:guid}")]
    public async Task<IActionResult> CreateSlideshowFromAlbumAsync(
        Guid albumId,
        [FromQuery] int intervalSeconds = 5,
        [FromQuery] SlideshowTransition transition = SlideshowTransition.Fade)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var slideshow = await _slideshowService.CreateFromAlbumAsync(albumId, intervalSeconds, transition);
            return Ok(Envelope(slideshow));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
    }

    /// <summary>Creates a slideshow from selected photo IDs.</summary>
    [HttpPost("slideshows/from-selection")]
    public async Task<IActionResult> CreateSlideshowFromSelectionAsync(
        [FromBody] CreateSlideshowFromSelectionRequest dto)
    {
        var caller = GetAuthenticatedCaller();
        var slideshow = await _slideshowService.CreateFromSelectionAsync(
            dto.PhotoIds, dto.IntervalSeconds, dto.Transition);
        return Ok(Envelope(slideshow));
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────

/// <summary>Request DTO for creating a photo record.</summary>
public sealed record CreatePhotoRequest
{
    /// <summary>The file node ID from the Files module.</summary>
    public Guid FileNodeId { get; init; }

    /// <summary>Original file name.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME type of the image.</summary>
    public required string MimeType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }
}

/// <summary>Request DTO for sharing a photo.</summary>
public sealed record CreatePhotoShareRequest
{
    /// <summary>User to share with (null for public link).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Permission level.</summary>
    public PhotoSharePermission Permission { get; init; }
}

/// <summary>Request DTO for creating a slideshow from selected photos.</summary>
public sealed record CreateSlideshowFromSelectionRequest
{
    /// <summary>Photo IDs to include in the slideshow.</summary>
    public required List<Guid> PhotoIds { get; init; }

    /// <summary>Seconds between slides.</summary>
    public int IntervalSeconds { get; init; } = 5;

    /// <summary>Transition effect.</summary>
    public SlideshowTransition Transition { get; init; } = SlideshowTransition.Fade;
}
