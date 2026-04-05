using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Photos.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Photos.Host.Controllers;

/// <summary>
/// REST API controller for album management.
/// </summary>
[Route("api/v1/albums")]
public class AlbumsController : PhotosControllerBase
{
    private readonly AlbumService _albumService;
    private readonly PhotoShareService _shareService;
    private readonly ILogger<AlbumsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumsController"/> class.
    /// </summary>
    public AlbumsController(
        AlbumService albumService,
        PhotoShareService shareService,
        ILogger<AlbumsController> logger)
    {
        _albumService = albumService;
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>Lists albums for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAlbumsAsync()
    {
        var caller = GetAuthenticatedCaller();
        var albums = await _albumService.ListAlbumsAsync(caller);
        return Ok(Envelope(albums));
    }

    /// <summary>Gets an album by ID.</summary>
    [HttpGet("{albumId:guid}")]
    public async Task<IActionResult> GetAlbumAsync(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        var album = await _albumService.GetAlbumAsync(albumId, caller);
        return album is null
            ? NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, "Album not found."))
            : Ok(Envelope(album));
    }

    /// <summary>Creates a new album.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAlbumAsync([FromBody] CreateAlbumDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var album = await _albumService.CreateAlbumAsync(dto, caller);
        return Created($"/api/v1/albums/{album.Id}", Envelope(album));
    }

    /// <summary>Updates an album.</summary>
    [HttpPut("{albumId:guid}")]
    public async Task<IActionResult> UpdateAlbumAsync(Guid albumId, [FromBody] UpdateAlbumDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var album = await _albumService.UpdateAlbumAsync(albumId, dto, caller);
            return Ok(Envelope(album));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
    }

    /// <summary>Deletes an album.</summary>
    [HttpDelete("{albumId:guid}")]
    public async Task<IActionResult> DeleteAlbumAsync(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _albumService.DeleteAlbumAsync(albumId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
    }

    /// <summary>Adds a photo to an album.</summary>
    [HttpPost("{albumId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> AddPhotoToAlbumAsync(Guid albumId, Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _albumService.AddPhotoToAlbumAsync(albumId, photoId, caller);
            return Ok(Envelope(new { added = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.PhotoAlreadyInAlbum)
        {
            return Conflict(ErrorEnvelope(ErrorCodes.PhotoAlreadyInAlbum, ex.Message));
        }
    }

    /// <summary>Removes a photo from an album.</summary>
    [HttpDelete("{albumId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> RemovePhotoFromAlbumAsync(Guid albumId, Guid photoId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _albumService.RemovePhotoFromAlbumAsync(albumId, photoId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
    }

    /// <summary>Gets photos in an album.</summary>
    [HttpGet("{albumId:guid}/photos")]
    public async Task<IActionResult> GetAlbumPhotosAsync(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        var album = await _albumService.GetAlbumAsync(albumId, caller);
        if (album is null)
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, "Album not found."));

        var photos = await _albumService.GetAlbumPhotosAsync(albumId, caller);
        return Ok(Envelope(photos));
    }

    /// <summary>Shares an album with a user.</summary>
    [HttpPost("{albumId:guid}/shares")]
    public async Task<IActionResult> ShareAlbumAsync(Guid albumId, [FromBody] CreateAlbumShareRequest dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var share = await _shareService.ShareAlbumAsync(
                albumId, dto.SharedWithUserId!.Value, dto.Permission, caller);
            return Created($"/api/v1/albums/{albumId}/shares/{share.Id}", Envelope(share));
        }
        catch (DotNetCloudException ex) when (ex.ErrorCode == ErrorCodes.AlbumNotFound)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.AlbumNotFound, ex.Message));
        }
    }

    /// <summary>Gets shares for an album.</summary>
    [HttpGet("{albumId:guid}/shares")]
    public async Task<IActionResult> GetAlbumSharesAsync(Guid albumId)
    {
        var caller = GetAuthenticatedCaller();
        var shares = await _shareService.GetAlbumSharesAsync(albumId, caller);
        return Ok(Envelope(shares));
    }
}

/// <summary>Request DTO for sharing an album.</summary>
public sealed record CreateAlbumShareRequest
{
    /// <summary>User to share with (null for public link).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Permission level.</summary>
    public PhotoSharePermission Permission { get; init; }
}
