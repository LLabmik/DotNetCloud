using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// Handles anonymous access to public link shares.
/// </summary>
[AllowAnonymous]
[Route("api/v1/public/shares")]
public class PublicShareController : FilesControllerBase
{
    private readonly IShareService _shareService;
    private readonly IFileService _fileService;
    private readonly IDownloadService _downloadService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicShareController"/> class.
    /// </summary>
    public PublicShareController(IShareService shareService, IFileService fileService, IDownloadService downloadService)
    {
        _shareService = shareService;
        _fileService = fileService;
        _downloadService = downloadService;
    }

    /// <summary>
    /// Resolves a public link token to its share metadata.
    /// Returns 404 if the token is invalid, expired, or the download limit is reached.
    /// Returns 401 if a password is required but not supplied or incorrect.
    /// </summary>
    /// <param name="token">The URL-safe link token.</param>
    /// <param name="password">Optional password for password-protected links.</param>
    [HttpGet("{token}")]
    public Task<IActionResult> ResolveAsync(string token, [FromQuery] string? password) => ExecuteAsync(async () =>
    {
        var share = await _shareService.ResolvePublicLinkAsync(token, password);

        if (share is null)
            return NotFound(ErrorEnvelope("SHARE_NOT_FOUND", "Public link not found, expired, or download limit reached."));

        return Ok(Envelope(share));
    });

    /// <summary>
    /// Downloads the file associated with a public link token.
    /// Returns 404 if the token is invalid, expired, or the download limit is reached.
    /// </summary>
    /// <param name="token">The URL-safe link token.</param>
    /// <param name="password">Optional password for password-protected links.</param>
    [HttpGet("{token}/download")]
    public Task<IActionResult> DownloadAsync(string token, [FromQuery] string? password) => ExecuteAsync(async () =>
    {
        var share = await _shareService.ResolvePublicLinkAsync(token, password);

        if (share is null)
            return NotFound(ErrorEnvelope("SHARE_NOT_FOUND", "Public link not found, expired, or download limit reached."));

        var systemCaller = CallerContext.CreateSystemContext();
        var node = await _fileService.GetNodeAsync(share.FileNodeId, systemCaller);
        if (node is null)
            return NotFound(ErrorEnvelope("FILE_NOT_FOUND", "The shared file no longer exists."));

        await _shareService.IncrementDownloadCountAsync(share.Id);
        var stream = await _downloadService.DownloadCurrentAsync(share.FileNodeId, systemCaller);
        var mime = string.IsNullOrWhiteSpace(node.MimeType) ? "application/octet-stream" : node.MimeType;
        return File(stream, mime, node.Name, enableRangeProcessing: true);
    });
}
