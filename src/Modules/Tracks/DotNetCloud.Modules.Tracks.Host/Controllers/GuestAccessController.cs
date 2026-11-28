using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for guest user management.
/// </summary>
[ApiController]
public class GuestAccessController : TracksControllerBase
{
    private readonly GuestAccessService _guestAccessService;
    private readonly ILogger<GuestAccessController> _logger;

    public GuestAccessController(GuestAccessService guestAccessService, ILogger<GuestAccessController> logger)
    {
        _guestAccessService = guestAccessService;
        _logger = logger;
    }

    /// <summary>Lists all guests for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/guests")]
    public async Task<IActionResult> GetGuestsAsync(Guid productId, CancellationToken ct)
    {
        var guests = await _guestAccessService.GetGuestsByProductAsync(productId, ct);
        return Ok(Envelope(guests));
    }

    /// <summary>Invites a guest to a product via email.</summary>
    [HttpPost("api/v1/products/{productId:guid}/guests")]
    public async Task<IActionResult> InviteGuestAsync(Guid productId, [FromBody] InviteGuestDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var guest = await _guestAccessService.InviteGuestAsync(productId, caller.UserId, dto, ct);
            return Created($"/api/v1/products/{productId}/guests", Envelope(guest));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Revokes a guest's access.</summary>
    [HttpDelete("api/v1/guests/{guestId:guid}")]
    public async Task<IActionResult> RevokeGuestAsync(Guid guestId, CancellationToken ct)
    {
        try
        {
            await _guestAccessService.RevokeGuestAsync(guestId, ct);
            return Ok(Envelope(new { revoked = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Grants a guest permission on a work item.</summary>
    [HttpPost("api/v1/guests/{guestId:guid}/work-items/{workItemId:guid}/permissions")]
    public async Task<IActionResult> GrantPermissionAsync(Guid guestId, Guid workItemId, [FromBody] GrantPermissionDto dto, CancellationToken ct)
    {
        var permission = dto.Permission?.ToLowerInvariant() == "comment"
            ? Models.GuestPermissionLevel.Comment
            : Models.GuestPermissionLevel.View;

        await _guestAccessService.GrantPermissionAsync(guestId, workItemId, permission, ct);
        return Ok(Envelope(new { granted = true }));
    }

    /// <summary>Revokes a guest's permission on a work item.</summary>
    [HttpDelete("api/v1/guests/{guestId:guid}/work-items/{workItemId:guid}/permissions")]
    public async Task<IActionResult> RevokePermissionAsync(Guid guestId, Guid workItemId, CancellationToken ct)
    {
        await _guestAccessService.RevokePermissionAsync(guestId, workItemId, ct);
        return Ok(Envelope(new { revoked = true }));
    }
}
