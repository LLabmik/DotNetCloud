using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for WOPI (Web Application Open Platform Interface) integration.
/// Enables Collabora Online/CODE to fetch and save files for browser-based document editing.
/// </summary>
/// <remarks>
/// WOPI protocol endpoints (called by Collabora, authenticated via access_token query parameter):
/// - GET    /api/v1/wopi/files/{fileId}           → CheckFileInfo (file metadata)
/// - GET    /api/v1/wopi/files/{fileId}/contents   → GetFile (download content)
/// - POST   /api/v1/wopi/files/{fileId}/contents   → PutFile (save edited content)
///
/// Token management endpoints (called by the DotNetCloud UI, authenticated via standard auth):
/// - POST   /api/v1/wopi/token/{fileId}            → Generate WOPI access token
/// - DELETE /api/v1/wopi/token/{fileId}            → End editing session
/// - GET    /api/v1/wopi/discovery                 → Check Collabora availability
/// - GET    /api/v1/wopi/discovery/supports/{ext}  → Check extension support
/// </remarks>
[ApiController]
[Route("api/v1/wopi")]
public class WopiController : FilesControllerBase
{
    private readonly IWopiService _wopiService;
    private readonly IWopiTokenService _tokenService;
    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly IWopiProofKeyValidator _proofKeyValidator;
    private readonly IWopiSessionTracker _sessionTracker;
    private readonly CollaboraOptions _collaboraOptions;
    private readonly ILogger<WopiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiController"/> class.
    /// </summary>
    public WopiController(
        IWopiService wopiService,
        IWopiTokenService tokenService,
        ICollaboraDiscoveryService discoveryService,
        IWopiProofKeyValidator proofKeyValidator,
        IWopiSessionTracker sessionTracker,
        IOptions<CollaboraOptions> collaboraOptions,
        ILogger<WopiController> logger)
    {
        _wopiService = wopiService;
        _tokenService = tokenService;
        _discoveryService = discoveryService;
        _proofKeyValidator = proofKeyValidator;
        _sessionTracker = sessionTracker;
        _collaboraOptions = collaboraOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// WOPI CheckFileInfo — Returns metadata about a file.
    /// Called by Collabora when opening a document for editing.
    /// Authenticated via the access_token query parameter.
    /// Also begins a session slot for concurrent session tracking.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("files/{fileId:guid}")]
    public async Task<IActionResult> CheckFileInfoAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        // Enforce concurrent session limit
        if (!_sessionTracker.TryBeginSession(fileId, tokenContext.UserId))
        {
            _logger.LogWarning("WOPI CheckFileInfo denied: concurrent session limit reached for file {FileId}.", fileId);
            return StatusCode(503); // Service Unavailable
        }

        var caller = WopiCaller(tokenContext.UserId);
        var result = await _wopiService.CheckFileInfoAsync(fileId, caller);

        if (result is null)
        {
            _sessionTracker.EndSession(fileId, tokenContext.UserId);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// WOPI GetFile — Returns the file content as a byte stream.
    /// Called by Collabora to load a document for editing.
    /// Authenticated via the access_token query parameter.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("files/{fileId:guid}/contents")]
    public async Task<IActionResult> GetFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        _sessionTracker.HeartbeatSession(fileId, tokenContext.UserId);

        var caller = WopiCaller(tokenContext.UserId);

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
    [AllowAnonymous]
    [HttpPost("files/{fileId:guid}/contents")]
    public async Task<IActionResult> PutFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        if (!tokenContext.CanWrite)
        {
            _logger.LogWarning("WOPI PutFile denied: token for user {UserId} on file {FileId} is read-only",
                tokenContext.UserId, fileId);
            return StatusCode(403);
        }

        _sessionTracker.HeartbeatSession(fileId, tokenContext.UserId);

        var caller = WopiCaller(tokenContext.UserId);

        try
        {
            var lastModifiedTime = await _wopiService.PutFileAsync(fileId, Request.Body, caller);
            return Ok(new { LastModifiedTime = lastModifiedTime });
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
    public Task<IActionResult> GenerateTokenAsync(Guid fileId) => ExecuteAsync(async () =>
    {
        var token = await _tokenService.GenerateTokenAsync(fileId, GetAuthenticatedCaller());
        return Ok(Envelope(token));
    });

    /// <summary>
    /// Ends the editing session for a file, freeing a concurrent session slot.
    /// Called by the DotNetCloud UI when the editor is closed.
    /// </summary>
    [HttpDelete("token/{fileId:guid}")]
    public IActionResult EndSessionAsync(Guid fileId)
    {
        var caller = GetAuthenticatedCaller();
        _sessionTracker.EndSession(fileId, caller.UserId);
        return NoContent();
    }

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

    /// <summary>
    /// Creates a <see cref="CallerContext"/> from a validated WOPI token.
    /// Unlike <see cref="FilesControllerBase.ToCaller"/>, this does not require HttpContext.User
    /// to be authenticated, since WOPI protocol callbacks use access_token query parameters.
    /// </summary>
    private static CallerContext WopiCaller(Guid userId)
    {
        return new CallerContext(userId, [], CallerType.User);
    }

    /// <summary>
    /// Validates the WOPI proof key headers when proof key validation is enabled.
    /// Returns <c>true</c> if validation passes or is disabled; <c>false</c> on failure.
    /// </summary>
    private async Task<bool> ValidateProofAsync(string accessToken)
    {
        if (!_collaboraOptions.EnableProofKeyValidation)
            return true;

        var proof = Request.Headers["X-WOPI-Proof"].FirstOrDefault();
        var proofOld = Request.Headers["X-WOPI-Proof-Old"].FirstOrDefault();
        var timestamp = Request.Headers["X-WOPI-TimeStamp"].FirstOrDefault();

        // If Collabora does not send proof headers, skip validation (allows non-Collabora clients)
        if (string.IsNullOrEmpty(proof) && string.IsNullOrEmpty(timestamp))
            return true;

        var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        return await _proofKeyValidator.ValidateAsync(accessToken, requestUrl, proof, proofOld, timestamp);
    }
}
