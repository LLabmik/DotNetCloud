using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for WOPI (Web Application Open Platform Interface) integration.
/// Enables Collabora Online/CODE to fetch and save files for browser-based document editing.
/// </summary>
/// <remarks>
/// WOPI protocol endpoints (called by Collabora, authenticated via access_token query parameter):
/// - GET  /api/v1/wopi/files/{fileId}           → CheckFileInfo (file metadata)
/// - GET  /api/v1/wopi/files/{fileId}/contents   → GetFile (download content)
/// - POST /api/v1/wopi/files/{fileId}/contents   → PutFile (save edited content)
///
/// Token management endpoints (called by the DotNetCloud UI, authenticated via standard auth):
/// - POST /api/v1/wopi/token/{fileId}            → Generate WOPI access token
/// - GET  /api/v1/wopi/discovery                 → Check Collabora availability
/// </remarks>
[ApiController]
[Route("api/v1/wopi")]
public class WopiController : FilesControllerBase
{
    private readonly IWopiService _wopiService;
    private readonly IWopiTokenService _tokenService;
    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly ILogger<WopiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiController"/> class.
    /// </summary>
    public WopiController(
        IWopiService wopiService,
        IWopiTokenService tokenService,
        ICollaboraDiscoveryService discoveryService,
        ILogger<WopiController> logger)
    {
        _wopiService = wopiService;
        _tokenService = tokenService;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// WOPI CheckFileInfo — Returns metadata about a file.
    /// Called by Collabora when opening a document for editing.
    /// Authenticated via the access_token query parameter.
    /// </summary>
    [HttpGet("files/{fileId:guid}")]
    public async Task<IActionResult> CheckFileInfoAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        var caller = ToCaller(tokenContext.UserId);
        var result = await _wopiService.CheckFileInfoAsync(fileId, caller);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// WOPI GetFile — Returns the file content as a byte stream.
    /// Called by Collabora to load a document for editing.
    /// Authenticated via the access_token query parameter.
    /// </summary>
    [HttpGet("files/{fileId:guid}/contents")]
    public async Task<IActionResult> GetFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        var caller = ToCaller(tokenContext.UserId);

        try
        {
            var result = await _wopiService.GetFileAsync(fileId, caller);
            if (result is null)
                return NotFound();

            var (content, mimeType, fileName) = result.Value;
            return File(content, mimeType, fileName);
        }
        catch (Core.Errors.ForbiddenException)
        {
            return StatusCode(403);
        }
    }

    /// <summary>
    /// WOPI PutFile — Saves edited file content from Collabora.
    /// Creates a new file version using the chunked upload pipeline.
    /// Authenticated via the access_token query parameter.
    /// </summary>
    [HttpPost("files/{fileId:guid}/contents")]
    public async Task<IActionResult> PutFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!tokenContext.CanWrite)
        {
            _logger.LogWarning("WOPI PutFile denied: token for user {UserId} on file {FileId} is read-only",
                tokenContext.UserId, fileId);
            return StatusCode(403);
        }

        var caller = ToCaller(tokenContext.UserId);

        try
        {
            await _wopiService.PutFileAsync(fileId, Request.Body, caller);
            return Ok();
        }
        catch (Core.Errors.NotFoundException)
        {
            return NotFound();
        }
        catch (Core.Errors.ForbiddenException)
        {
            return StatusCode(403);
        }
        catch (Core.Errors.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>
    /// Generates a WOPI access token for a file.
    /// Called by the DotNetCloud UI before opening the Collabora editor.
    /// Returns the token, editor URL, and expiry information.
    /// </summary>
    [HttpPost("token/{fileId:guid}")]
    public Task<IActionResult> GenerateTokenAsync(Guid fileId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var token = await _tokenService.GenerateTokenAsync(fileId, ToCaller(userId));
        return Ok(Envelope(token));
    });

    /// <summary>
    /// Returns Collabora discovery information including available file formats and availability status.
    /// </summary>
    [HttpGet("discovery")]
    public async Task<IActionResult> GetDiscoveryAsync()
    {
        var discovery = await _discoveryService.DiscoverAsync();
        return Ok(Envelope(new
        {
            available = discovery.IsAvailable,
            supportedExtensions = discovery.Actions
                .Select(a => a.Extension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order()
                .ToList(),
            actionCount = discovery.Actions.Count,
            fetchedAt = discovery.FetchedAt
        }));
    }

    /// <summary>
    /// Checks whether a specific file extension is supported for online editing.
    /// </summary>
    [HttpGet("discovery/supports/{extension}")]
    public async Task<IActionResult> CheckExtensionSupportAsync(string extension)
    {
        var isSupported = await _discoveryService.IsSupportedExtensionAsync(extension);
        return Ok(Envelope(new { extension, supported = isSupported }));
    }
}
