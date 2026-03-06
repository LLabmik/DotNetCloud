using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Core-server-hosted WOPI endpoints for Collabora integration.
/// This keeps document editing available in single-process installs where module-host routing is not configured.
/// </summary>
[ApiController]
[Route("api/v1/wopi")]
public sealed class WopiController : ControllerBase
{
    private readonly IWopiService _wopiService;
    private readonly IWopiTokenService _tokenService;
    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly IWopiProofKeyValidator _proofKeyValidator;
    private readonly IWopiSessionTracker _sessionTracker;
    private readonly CollaboraOptions _collaboraOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="WopiController"/> class.
    /// </summary>
    public WopiController(
        IWopiService wopiService,
        IWopiTokenService tokenService,
        ICollaboraDiscoveryService discoveryService,
        IWopiProofKeyValidator proofKeyValidator,
        IWopiSessionTracker sessionTracker,
        IOptions<CollaboraOptions> collaboraOptions)
    {
        _wopiService = wopiService;
        _tokenService = tokenService;
        _discoveryService = discoveryService;
        _proofKeyValidator = proofKeyValidator;
        _sessionTracker = sessionTracker;
        _collaboraOptions = collaboraOptions.Value;
    }

    /// <summary>
    /// WOPI CheckFileInfo endpoint used by Collabora to fetch file metadata.
    /// </summary>
    [HttpGet("files/{fileId:guid}")]
    public async Task<IActionResult> CheckFileInfoAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        if (!_sessionTracker.TryBeginSession(fileId, tokenContext.UserId))
            return StatusCode(503);

        var result = await _wopiService.CheckFileInfoAsync(fileId, ToCaller(tokenContext.UserId));
        if (result is null)
        {
            _sessionTracker.EndSession(fileId, tokenContext.UserId);
            return NotFound();
        }

        // Must be raw WOPI JSON (not wrapped), handled by response-envelope exclusion.
        return Ok(result);
    }

    /// <summary>
    /// WOPI GetFile endpoint used by Collabora to download file contents.
    /// </summary>
    [HttpGet("files/{fileId:guid}/contents")]
    public async Task<IActionResult> GetFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        _sessionTracker.HeartbeatSession(fileId, tokenContext.UserId);

        try
        {
            var result = await _wopiService.GetFileAsync(fileId, ToCaller(tokenContext.UserId));
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
    /// WOPI PutFile endpoint used by Collabora to save edited file contents.
    /// </summary>
    [HttpPost("files/{fileId:guid}/contents")]
    public async Task<IActionResult> PutFileAsync(Guid fileId, [FromQuery] string access_token)
    {
        var tokenContext = _tokenService.ValidateToken(access_token, fileId);
        if (tokenContext is null)
            return Unauthorized();

        if (!await ValidateProofAsync(access_token))
            return Unauthorized();

        if (!tokenContext.CanWrite)
            return StatusCode(403);

        _sessionTracker.HeartbeatSession(fileId, tokenContext.UserId);

        try
        {
            var lastModifiedTime = await _wopiService.PutFileAsync(fileId, Request.Body, ToCaller(tokenContext.UserId));
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
            return BadRequest(new { success = false, error = new { code = ex.ErrorCode, message = ex.Message } });
        }
    }

    /// <summary>
    /// Generates a WOPI access token for a file.
    /// </summary>
    [HttpPost("token/{fileId:guid}")]
    public async Task<IActionResult> GenerateTokenAsync(Guid fileId, [FromQuery] Guid userId)
    {
        var effectiveUserId = ResolveUserId(userId);
        if (effectiveUserId == Guid.Empty)
            return Unauthorized(new { success = false, error = new { code = "AUTH_FORBIDDEN", message = "Authentication is required." } });

        try
        {
            var token = await _tokenService.GenerateTokenAsync(fileId, ToCaller(effectiveUserId));
            return Ok(new { success = true, data = token });
        }
        catch (Core.Errors.NotFoundException ex)
        {
            return NotFound(new { success = false, error = new { code = ex.ErrorCode, message = ex.Message } });
        }
        catch (Core.Errors.ForbiddenException ex)
        {
            return StatusCode(403, new { success = false, error = new { code = ex.ErrorCode, message = ex.Message } });
        }
        catch (Core.Errors.InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = ex.ErrorCode, message = ex.Message } });
        }
    }

    /// <summary>
    /// Ends an active WOPI editing session.
    /// </summary>
    [HttpDelete("token/{fileId:guid}")]
    public IActionResult EndSessionAsync(Guid fileId, [FromQuery] Guid userId)
    {
        var effectiveUserId = ResolveUserId(userId);
        if (effectiveUserId == Guid.Empty)
            return Unauthorized(new { success = false, error = new { code = "AUTH_FORBIDDEN", message = "Authentication is required." } });

        _sessionTracker.EndSession(fileId, effectiveUserId);
        return NoContent();
    }

    /// <summary>
    /// Returns Collabora discovery status and supported file extensions.
    /// </summary>
    [HttpGet("discovery")]
    public async Task<IActionResult> GetDiscoveryAsync()
    {
        var discovery = await _discoveryService.DiscoverAsync();
        return Ok(new
        {
            success = true,
            data = new
            {
                available = discovery.IsAvailable,
                supportedExtensions = discovery.Actions
                    .Select(a => a.Extension)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order()
                    .ToList(),
                actionCount = discovery.Actions.Count,
                fetchedAt = discovery.FetchedAt
            }
        });
    }

    /// <summary>
    /// Checks whether a file extension is supported by Collabora.
    /// </summary>
    [HttpGet("discovery/supports/{extension}")]
    public async Task<IActionResult> CheckExtensionSupportAsync(string extension)
    {
        var isSupported = await _discoveryService.IsSupportedExtensionAsync(extension);
        return Ok(new { success = true, data = new { extension, supported = isSupported } });
    }

    private CallerContext ToCaller(Guid userId) => new(userId, [], CallerType.User);

    private Guid ResolveUserId(Guid userId)
    {
        if (userId != Guid.Empty)
            return userId;

        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claimValue, out var parsed) ? parsed : Guid.Empty;
    }

    private async Task<bool> ValidateProofAsync(string accessToken)
    {
        // Temporary hard bypass for environments where reverse-proxy/proof-key handling is unstable.
        // Collabora access is still gated by signed, file-scoped WOPI access tokens.
        // Set DOTNETCLOUD_ENABLE_WOPI_PROOF_VALIDATION=true to re-enable strict proof validation.
        var strictProofValidationEnabled = bool.TryParse(
            Environment.GetEnvironmentVariable("DOTNETCLOUD_ENABLE_WOPI_PROOF_VALIDATION"),
            out var strictEnabled) && strictEnabled;

        if (!strictProofValidationEnabled)
            return true;

        if (!_collaboraOptions.EnableProofKeyValidation)
            return true;

        var proof = Request.Headers["X-WOPI-Proof"].FirstOrDefault();
        var proofOld = Request.Headers["X-WOPI-Proof-Old"].FirstOrDefault();
        var timestamp = Request.Headers["X-WOPI-TimeStamp"].FirstOrDefault();

        if (string.IsNullOrEmpty(proof) && string.IsNullOrEmpty(timestamp))
            return true;

        var requestUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
        return await _proofKeyValidator.ValidateAsync(accessToken, requestUrl, proof, proofOld, timestamp);
    }
}
