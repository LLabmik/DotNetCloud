using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// REST API controller for file and folder operations.
/// Provides CRUD, tree browsing, move, copy, upload, download, and favorites.
/// </summary>
[Route("api/v1/files")]
public sealed class FilesController : FilesControllerBase
{
    private const string DefaultDownloadMimeType = "application/octet-stream";

    private readonly IFileService _fileService;
    private readonly IChunkedUploadService _uploadService;
    private readonly IDownloadService _downloadService;
    private readonly IVersionService _versionService;
    private readonly IShareService _shareService;
    private readonly FileSystemOptions _fileSystemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesController"/> class.
    /// </summary>
    public FilesController(
        IFileService fileService,
        IChunkedUploadService uploadService,
        IDownloadService downloadService,
        IVersionService versionService,
        IShareService shareService,
        IOptions<FileSystemOptions> fileSystemOptions)
    {
        _fileService = fileService;
        _uploadService = uploadService;
        _downloadService = downloadService;
        _versionService = versionService;
        _shareService = shareService;
        _fileSystemOptions = fileSystemOptions.Value;
    }

    /// <summary>
    /// Lists files and folders in a directory.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(
        [FromQuery] Guid? parentId) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var nodes = parentId.HasValue
            ? await _fileService.ListChildrenAsync(parentId.Value, caller)
            : await _fileService.ListRootAsync(caller);

        return Ok(nodes);
    });

    /// <summary>
    /// Gets a file or folder by ID.
    /// </summary>
    [HttpGet("{nodeId:guid}")]
    public Task<IActionResult> GetAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.GetNodeAsync(nodeId, GetAuthenticatedCaller());
        return node is null
            ? NotFound(ErrorEnvelope("not_found", "Node not found."))
            : Ok(node);
    });

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    [HttpPost("folders")]
    public Task<IActionResult> CreateFolderAsync(
        [FromBody] CreateFolderDto dto) => ExecuteAsync(async () =>
    {
        var folder = await _fileService.CreateFolderAsync(dto, GetAuthenticatedCaller());
        return Created($"/api/v1/files/{folder.Id}", folder);
    });

    /// <summary>
    /// Renames a file or folder.
    /// </summary>
    [HttpPut("{nodeId:guid}/rename")]
    public Task<IActionResult> RenameAsync(Guid nodeId, [FromBody] RenameNodeDto dto) => ExecuteAsync(async () =>
    {
        var node = await _fileService.RenameAsync(nodeId, dto, GetAuthenticatedCaller());
        return Ok(node);
    });

    /// <summary>
    /// Moves a file or folder to a different parent.
    /// </summary>
    [HttpPut("{nodeId:guid}/move")]
    public Task<IActionResult> MoveAsync(Guid nodeId, [FromBody] MoveNodeDto dto) => ExecuteAsync(async () =>
    {
        var node = await _fileService.MoveAsync(nodeId, dto, GetAuthenticatedCaller());
        return Ok(node);
    });

    /// <summary>
    /// Copies a file or folder to a target parent.
    /// </summary>
    [HttpPost("{nodeId:guid}/copy")]
    public Task<IActionResult> CopyAsync(Guid nodeId, [FromBody] MoveNodeDto dto) => ExecuteAsync(async () =>
    {
        var copy = await _fileService.CopyAsync(nodeId, dto.TargetParentId, GetAuthenticatedCaller());
        return Created($"/api/v1/files/{copy.Id}", copy);
    });

    /// <summary>
    /// Moves a file or folder to trash (soft-delete).
    /// </summary>
    [HttpDelete("{nodeId:guid}")]
    public Task<IActionResult> DeleteAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        await _fileService.DeleteAsync(nodeId, GetAuthenticatedCaller());
        return Ok(new { deleted = true });
    });

    /// <summary>
    /// Toggles favorite status on a file or folder.
    /// </summary>
    [HttpPost("{nodeId:guid}/favorite")]
    public Task<IActionResult> ToggleFavoriteAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        var node = await _fileService.ToggleFavoriteAsync(nodeId, GetAuthenticatedCaller());
        return Ok(new { isFavorite = node.IsFavorite });
    });

    /// <summary>
    /// Lists user's favorite files and folders.
    /// </summary>
    [HttpGet("favorites")]
    public Task<IActionResult> ListFavoritesAsync() => ExecuteAsync(async () =>
    {
        var favorites = await _fileService.ListFavoritesAsync(GetAuthenticatedCaller());
        return Ok(favorites);
    });

    /// <summary>
    /// Lists recently updated files.
    /// </summary>
    [HttpGet("recent")]
    public Task<IActionResult> ListRecentAsync([FromQuery] int count = 20) => ExecuteAsync(async () =>
    {
        var recent = await _fileService.ListRecentAsync(count, GetAuthenticatedCaller());
        return Ok(recent);
    });

    /// <summary>
    /// Searches for files and folders by name.
    /// </summary>
    [HttpGet("search")]
    public Task<IActionResult> SearchAsync(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) => ExecuteAsync(async () =>
    {
        var result = await _fileService.SearchAsync(query, page, pageSize, GetAuthenticatedCaller());
        return Ok(new { items = result.Items, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount, totalPages = result.TotalPages });
    });

    /// <summary>
    /// Initiates a chunked upload session.
    /// </summary>
    [HttpPost("upload/initiate")]
    [EnableRateLimiting("module-upload-initiate")]
    public Task<IActionResult> InitiateUploadAsync([FromBody] InitiateUploadDto dto) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        if (!string.IsNullOrEmpty(dto.FileName) && dto.FileName.Length > _fileSystemOptions.MaxPathWarningThreshold)
            Response.Headers["X-Path-Warning"] = "path-length-exceeds-windows-limit";
        var session = await _uploadService.InitiateUploadAsync(dto, caller);
        return Created($"/api/v1/files/upload/{session.SessionId}", session);
    });

    /// <summary>
    /// Uploads a single chunk.
    /// </summary>
    [HttpPut("upload/{sessionId:guid}/chunks/{chunkHash}")]
    [EnableRateLimiting("module-upload-chunks")]
    public Task<IActionResult> UploadChunkAsync(Guid sessionId, string chunkHash) => ExecuteAsync(async () =>
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        await _uploadService.UploadChunkAsync(sessionId, chunkHash, ms.ToArray(), GetAuthenticatedCaller());
        return Ok(new { uploaded = true });
    });

    /// <summary>
    /// Completes an upload session.
    /// </summary>
    [HttpPost("upload/{sessionId:guid}/complete")]
    public Task<IActionResult> CompleteUploadAsync(Guid sessionId) => ExecuteAsync(async () =>
    {
        var node = await _uploadService.CompleteUploadAsync(sessionId, GetAuthenticatedCaller());
        return Ok(node);
    });

    /// <summary>
    /// Cancels an upload session.
    /// </summary>
    [HttpDelete("upload/{sessionId:guid}")]
    public Task<IActionResult> CancelUploadAsync(Guid sessionId) => ExecuteAsync(async () =>
    {
        await _uploadService.CancelUploadAsync(sessionId, GetAuthenticatedCaller());
        return Ok(new { cancelled = true });
    });

    /// <summary>
    /// Gets the status of an upload session.
    /// </summary>
    [HttpGet("upload/{sessionId:guid}")]
    public Task<IActionResult> GetUploadSessionAsync(Guid sessionId) => ExecuteAsync(async () =>
    {
        var session = await _uploadService.GetSessionAsync(sessionId, GetAuthenticatedCaller());
        return session is null
            ? NotFound(ErrorEnvelope("not_found", "Upload session not found."))
            : Ok(session);
    });

    /// <summary>
    /// Downloads a file. Optionally specify a version number.
    /// </summary>
    [HttpGet("{nodeId:guid}/download")]
    [EnableRateLimiting("module-download")]
    public Task<IActionResult> DownloadAsync(Guid nodeId, [FromQuery] int? version = null) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();

        if (version.HasValue)
        {
            var ver = await _versionService.GetVersionByNumberAsync(nodeId, version.Value, caller);
            if (ver is null)
                return NotFound(ErrorEnvelope("not_found", "Version not found."));

            var versionedNode = await _fileService.GetNodeAsync(nodeId, caller);
            var stream = await _downloadService.DownloadVersionAsync(ver.Id, caller);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, NormalizeDownloadMimeType(ver.MimeType), versionedNode?.Name);
        }

        var node = await _fileService.GetNodeAsync(nodeId, caller);
        if (node is null)
            return NotFound(ErrorEnvelope("not_found", "Node not found."));

        var downloadStream = await _downloadService.DownloadCurrentAsync(nodeId, caller);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(downloadStream, NormalizeDownloadMimeType(node.MimeType), node.Name, enableRangeProcessing: true);
    });

    private static string NormalizeDownloadMimeType(string? mimeType)
    {
        return string.IsNullOrWhiteSpace(mimeType) ? DefaultDownloadMimeType : mimeType;
    }

    /// <summary>
    /// Gets the chunk manifest (ordered hashes) for a file.
    /// </summary>
    [HttpGet("{nodeId:guid}/chunks")]
    [EnableRateLimiting("module-chunks")]
    public Task<IActionResult> GetChunkManifestAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        var manifest = await _downloadService.GetChunkManifestAsync(nodeId, GetAuthenticatedCaller());
        return Ok(manifest);
    });

    /// <summary>
    /// Downloads a raw chunk by its SHA-256 hash. Used by sync clients for efficient chunk retrieval.
    /// </summary>
    [HttpGet("chunks/{chunkHash}")]
    [EnableRateLimiting("module-chunks")]
    public Task<IActionResult> DownloadChunkByHashAsync(string chunkHash) => ExecuteAsync(async () =>
    {
        var stream = await _downloadService.DownloadChunkByHashAsync(chunkHash, GetAuthenticatedCaller());
        if (stream is null)
            return NotFound(ErrorEnvelope("not_found", "Chunk not found."));
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "application/octet-stream", chunkHash, enableRangeProcessing: false);
    });

    /// <summary>
    /// Lists files shared with the current user.
    /// </summary>
    [HttpGet("shared-with-me")]
    public Task<IActionResult> GetSharedWithMeAsync() => ExecuteAsync(async () =>
    {
        var shares = await _shareService.GetSharedWithMeAsync(GetAuthenticatedCaller());
        return Ok(shares);
    });

    /// <summary>
    /// Resolves a public share link.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public/{linkToken}")]
    public Task<IActionResult> ResolvePublicLinkAsync(string linkToken, [FromQuery] string? password = null) => ExecuteAsync(async () =>
    {
        var share = await _shareService.ResolvePublicLinkAsync(linkToken, password);
        return share is null
            ? NotFound(ErrorEnvelope("not_found", "Public link not found or expired."))
            : Ok(share);
    });
}
