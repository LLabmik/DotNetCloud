using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file and folder operations.
/// Provides CRUD, tree browsing, move, copy, upload, download, and favorites.
/// </summary>
[Route("api/v1/files")]
public class FilesController : FilesControllerBase
{
    private readonly IFileService _fileService;
    private readonly IChunkedUploadService _uploadService;
    private readonly IDownloadService _downloadService;
    private readonly IVersionService _versionService;
    private readonly IShareService _shareService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesController"/> class.
    /// </summary>
    public FilesController(
        IFileService fileService,
        IChunkedUploadService uploadService,
        IDownloadService downloadService,
        IVersionService versionService,
        IShareService shareService)
    {
        _fileService = fileService;
        _uploadService = uploadService;
        _downloadService = downloadService;
        _versionService = versionService;
        _shareService = shareService;
    }

    /// <summary>
    /// Lists files and folders in a directory.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery] Guid? parentId,
        [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);
        var nodes = parentId.HasValue
            ? await _fileService.ListChildrenAsync(parentId.Value, caller)
            : await _fileService.ListRootAsync(caller);

        return Ok(Envelope(nodes));
    });

    /// <summary>
    /// Gets a file or folder by ID.
    /// </summary>
    [HttpGet("{nodeId:guid}")]
    public Task<IActionResult> GetAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.GetNodeAsync(nodeId, ToCaller(userId));
        return node is null
            ? NotFound(ErrorEnvelope("not_found", "Node not found."))
            : Ok(Envelope(node));
    });

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [HttpPost("folders")]
    public Task<IActionResult> CreateFolderAsync(
        [FromBody] CreateFolderDto dto,
        [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var folder = await _fileService.CreateFolderAsync(dto, ToCaller(userId));
        return Created($"/api/v1/files/{folder.Id}", Envelope(folder));
    });

    /// <summary>
    /// Renames a file or folder.
    /// </summary>
    [HttpPut("{nodeId:guid}/rename")]
    public Task<IActionResult> RenameAsync(Guid nodeId, [FromBody] RenameNodeDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.RenameAsync(nodeId, dto, ToCaller(userId));
        return Ok(Envelope(node));
    });

    /// <summary>
    /// Moves a file or folder to a different parent.
    /// </summary>
    [HttpPut("{nodeId:guid}/move")]
    public Task<IActionResult> MoveAsync(Guid nodeId, [FromBody] MoveNodeDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.MoveAsync(nodeId, dto, ToCaller(userId));
        return Ok(Envelope(node));
    });

    /// <summary>
    /// Copies a file or folder to a target parent.
    /// </summary>
    [HttpPost("{nodeId:guid}/copy")]
    public Task<IActionResult> CopyAsync(Guid nodeId, [FromBody] MoveNodeDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var copy = await _fileService.CopyAsync(nodeId, dto.TargetParentId, ToCaller(userId));
        return Created($"/api/v1/files/{copy.Id}", Envelope(copy));
    });

    /// <summary>
    /// Moves a file or folder to trash (soft-delete).
    /// </summary>
    [HttpDelete("{nodeId:guid}")]
    public Task<IActionResult> DeleteAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _fileService.DeleteAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(new { deleted = true }));
    });

    /// <summary>
    /// Toggles favorite status on a file or folder.
    /// </summary>
    [HttpPost("{nodeId:guid}/favorite")]
    public Task<IActionResult> ToggleFavoriteAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.ToggleFavoriteAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(new { isFavorite = node.IsFavorite }));
    });

    /// <summary>
    /// Lists user's favorite files and folders.
    /// </summary>
    [HttpGet("favorites")]
    public Task<IActionResult> ListFavoritesAsync([FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var favorites = await _fileService.ListFavoritesAsync(ToCaller(userId));
        return Ok(Envelope(favorites));
    });

    /// <summary>
    /// Lists recently updated files.
    /// </summary>
    [HttpGet("recent")]
    public Task<IActionResult> ListRecentAsync([FromQuery] Guid userId, [FromQuery] int count = 20) => ExecuteAsync(async () =>
    {
        var recent = await _fileService.ListRecentAsync(count, ToCaller(userId));
        return Ok(Envelope(recent));
    });

    /// <summary>
    /// Searches for files and folders by name.
    /// </summary>
    [HttpGet("search")]
    public Task<IActionResult> SearchAsync(
        [FromQuery] string query,
        [FromQuery] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) => ExecuteAsync(async () =>
    {
        var result = await _fileService.SearchAsync(query, page, pageSize, ToCaller(userId));
        return Ok(Envelope(result.Items, new { result.Page, result.PageSize, result.TotalCount, result.TotalPages }));
    });

    /// <summary>
    /// Initiates a chunked upload session.
    /// </summary>
    [HttpPost("upload/initiate")]
    public Task<IActionResult> InitiateUploadAsync([FromBody] InitiateUploadDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var session = await _uploadService.InitiateUploadAsync(dto, ToCaller(userId));
        return Created($"/api/v1/files/upload/{session.SessionId}", Envelope(session));
    });

    /// <summary>
    /// Uploads a single chunk.
    /// </summary>
    [HttpPut("upload/{sessionId:guid}/chunks/{chunkHash}")]
    public Task<IActionResult> UploadChunkAsync(Guid sessionId, string chunkHash, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        await _uploadService.UploadChunkAsync(sessionId, chunkHash, ms.ToArray(), ToCaller(userId));
        return Ok(Envelope(new { uploaded = true }));
    });

    /// <summary>
    /// Completes an upload session.
    /// </summary>
    [HttpPost("upload/{sessionId:guid}/complete")]
    public Task<IActionResult> CompleteUploadAsync(Guid sessionId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var node = await _uploadService.CompleteUploadAsync(sessionId, ToCaller(userId));
        return Ok(Envelope(node));
    });

    /// <summary>
    /// Cancels an upload session.
    /// </summary>
    [HttpDelete("upload/{sessionId:guid}")]
    public Task<IActionResult> CancelUploadAsync(Guid sessionId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _uploadService.CancelUploadAsync(sessionId, ToCaller(userId));
        return Ok(Envelope(new { cancelled = true }));
    });

    /// <summary>
    /// Gets the status of an upload session.
    /// </summary>
    [HttpGet("upload/{sessionId:guid}")]
    public Task<IActionResult> GetUploadSessionAsync(Guid sessionId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var session = await _uploadService.GetSessionAsync(sessionId, ToCaller(userId));
        return session is null
            ? NotFound(ErrorEnvelope("not_found", "Upload session not found."))
            : Ok(Envelope(session));
    });

    /// <summary>
    /// Downloads a file. Optionally specify a version number.
    /// </summary>
    [HttpGet("{nodeId:guid}/download")]
    public Task<IActionResult> DownloadAsync(Guid nodeId, [FromQuery] Guid userId, [FromQuery] int? version = null) => ExecuteAsync(async () =>
    {
        var caller = ToCaller(userId);

        if (version.HasValue)
        {
            var ver = await _versionService.GetVersionByNumberAsync(nodeId, version.Value, caller);
            if (ver is null)
                return NotFound(ErrorEnvelope("not_found", "Version not found."));

            var stream = await _downloadService.DownloadVersionAsync(ver.Id, caller);
            return File(stream, ver.MimeType ?? "application/octet-stream");
        }

        var node = await _fileService.GetNodeAsync(nodeId, caller);
        if (node is null)
            return NotFound(ErrorEnvelope("not_found", "Node not found."));

        var downloadStream = await _downloadService.DownloadCurrentAsync(nodeId, caller);
        return File(downloadStream, node.MimeType ?? "application/octet-stream", node.Name, enableRangeProcessing: false);
    });

    /// <summary>
    /// Gets the chunk manifest (ordered hashes) for a file.
    /// </summary>
    [HttpGet("{nodeId:guid}/chunks")]
    public Task<IActionResult> GetChunkManifestAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var manifest = await _downloadService.GetChunkManifestAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(manifest));
    });

    /// <summary>
    /// Downloads a raw chunk by its SHA-256 hash. Used by sync clients for efficient chunk retrieval.
    /// </summary>
    [HttpGet("chunks/{chunkHash}")]
    public Task<IActionResult> DownloadChunkByHashAsync(string chunkHash, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var stream = await _downloadService.DownloadChunkByHashAsync(chunkHash, ToCaller(userId));
        return stream is null
            ? NotFound(ErrorEnvelope("not_found", "Chunk not found."))
            : File(stream, "application/octet-stream", enableRangeProcessing: false);
    });

    /// <summary>
    /// Lists files shared with the current user.
    /// </summary>
    [HttpGet("shared-with-me")]
    public Task<IActionResult> GetSharedWithMeAsync([FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var shares = await _shareService.GetSharedWithMeAsync(ToCaller(userId));
        return Ok(Envelope(shares));
    });

    /// <summary>
    /// Resolves a public share link.
    /// </summary>
    [HttpGet("public/{linkToken}")]
    public Task<IActionResult> ResolvePublicLinkAsync(string linkToken, [FromQuery] string? password = null) => ExecuteAsync(async () =>
    {
        var share = await _shareService.ResolvePublicLinkAsync(linkToken, password);
        return share is null
            ? NotFound(ErrorEnvelope("not_found", "Public link not found or expired."))
            : Ok(Envelope(share));
    });
}
