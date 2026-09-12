using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Endpoints used by logged-in clients to display and dismiss administrator broadcasts.
/// </summary>
[ApiController]
[Route("api/v1/core/broadcasts")]
[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
public sealed class BroadcastsController : ControllerBase
{
    private readonly IAdminBroadcastService _broadcastService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BroadcastsController"/> class.
    /// </summary>
    public BroadcastsController(IAdminBroadcastService broadcastService)
    {
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
    }

    /// <summary>
    /// Gets the active broadcast for the current user, or <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// Called when a client connects so users who log in after the message was sent (or who
    /// reconnect after a restart) still see it. Already-dismissed broadcasts are not returned.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active broadcast, or <see langword="null"/>.</returns>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var broadcast = await _broadcastService.GetActiveForUserAsync(userId, cancellationToken);
        return Ok(new { success = true, data = broadcast });
    }

    /// <summary>
    /// Records that the current user dismissed a broadcast so it is never shown to them again.
    /// </summary>
    /// <param name="id">The dismissed broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> DismissAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        await _broadcastService.DismissAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
