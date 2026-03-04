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

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicShareController"/> class.
    /// </summary>
    public PublicShareController(IShareService shareService)
    {
        _shareService = shareService;
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
}
