using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for work item share links.
/// </summary>
[ApiController]
public class ShareLinksController : TracksControllerBase
{
    private readonly ShareLinkService _shareLinkService;
    private readonly ILogger<ShareLinksController> _logger;

    public ShareLinksController(ShareLinkService shareLinkService, ILogger<ShareLinksController> logger)
    {
        _shareLinkService = shareLinkService;
        _logger = logger;
    }

    /// <summary>Lists all active share links for a work item.</summary>
    [HttpGet("api/v1/work-items/{workItemId:guid}/share-links")]
    public async Task<IActionResult> GetShareLinksAsync(Guid workItemId, CancellationToken ct)
    {
        var links = await _shareLinkService.GetShareLinksByWorkItemAsync(workItemId, ct);
        return Ok(Envelope(links));
    }

    /// <summary>Generates a new share link for a work item.</summary>
    [HttpPost("api/v1/work-items/{workItemId:guid}/share-links")]
    public async Task<IActionResult> CreateShareLinkAsync(Guid workItemId, [FromBody] CreateShareLinkDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var link = await _shareLinkService.GenerateShareLinkAsync(workItemId, caller.UserId, dto, ct);
            return Created($"/api/v1/work-items/{workItemId}/share-links", Envelope(link));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Revokes a share link.</summary>
    [HttpDelete("api/v1/share-links/{linkId:guid}")]
    public async Task<IActionResult> RevokeShareLinkAsync(Guid linkId, CancellationToken ct)
    {
        try
        {
            await _shareLinkService.RevokeShareLinkAsync(linkId, ct);
            return Ok(Envelope(new { revoked = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }
}
