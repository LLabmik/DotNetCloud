using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Public endpoints for checking available DotNetCloud updates.
/// No authentication required so that clients can check before login.
/// </summary>
[ApiController]
[Route("api/v1/core/updates")]
public class UpdateController : ControllerBase
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdateController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateController"/> class.
    /// </summary>
    public UpdateController(IUpdateService updateService, ILogger<UpdateController> logger)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks whether a newer version of DotNetCloud is available.
    /// </summary>
    /// <param name="currentVersion">Optional version to compare against (defaults to the server version).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="UpdateCheckResult"/> with availability information.</returns>
    [HttpGet("check")]
    public async Task<IActionResult> CheckForUpdateAsync(
        [FromQuery] string? currentVersion = null,
        CancellationToken ct = default)
    {
        var result = await _updateService.CheckForUpdateAsync(currentVersion, ct);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Returns a list of recent releases.
    /// </summary>
    /// <param name="count">Maximum number of releases to return (default 5, max 20).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("releases")]
    public async Task<IActionResult> GetRecentReleasesAsync(
        [FromQuery] int count = 5,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 20);
        var releases = await _updateService.GetRecentReleasesAsync(count, ct);
        return Ok(new { success = true, data = releases });
    }

    /// <summary>
    /// Returns the latest release.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("releases/latest")]
    public async Task<IActionResult> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        var release = await _updateService.GetLatestReleaseAsync(ct);
        if (release is null)
        {
            return NotFound(new { success = false, error = new { code = "NO_RELEASES", message = "No releases found." } });
        }

        return Ok(new { success = true, data = release });
    }
}
