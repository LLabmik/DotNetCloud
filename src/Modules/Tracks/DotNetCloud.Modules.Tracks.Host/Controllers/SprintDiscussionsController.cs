using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API endpoints for sprint planning and review session discussions.
/// </summary>
[ApiController]
public class SprintDiscussionsController : TracksControllerBase
{
    private readonly SprintDiscussionService _discussionService;
    private readonly ILogger<SprintDiscussionsController> _logger;

    public SprintDiscussionsController(
        SprintDiscussionService discussionService,
        ILogger<SprintDiscussionsController> logger)
    {
        _discussionService = discussionService;
        _logger = logger;
    }

    /// <summary>
    /// Lists discussion messages for a sprint, newest first.
    /// </summary>
    [HttpGet("api/v1/sprints/{sprintId:guid}/discussions")]
    public async Task<IActionResult> ListSprintDiscussions(
        Guid sprintId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var messages = await _discussionService.GetSprintMessagesAsync(sprintId, skip, take, ct);
        return Ok(Envelope(messages));
    }

    /// <summary>
    /// Sends a discussion message to a sprint.
    /// </summary>
    [HttpPost("api/v1/sprints/{sprintId:guid}/discussions")]
    public async Task<IActionResult> SendSprintDiscussion(
        Guid sprintId, [FromBody] SendSprintDiscussionDto dto, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var displayName = User.Identity?.Name ?? "Unknown";

        try
        {
            var message = await _discussionService.SendSprintMessageAsync(
                sprintId, caller.UserId, displayName, dto.Content, ct);
            return Created($"/api/v1/sprints/{sprintId}/discussions", Envelope(message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>
    /// Lists discussion messages for a review session, newest first.
    /// </summary>
    [HttpGet("api/v1/reviews/{reviewSessionId:guid}/discussions")]
    public async Task<IActionResult> ListReviewDiscussions(
        Guid reviewSessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var messages = await _discussionService.GetReviewSessionMessagesAsync(reviewSessionId, skip, take, ct);
        return Ok(Envelope(messages));
    }

    /// <summary>
    /// Sends a discussion message to a review session.
    /// </summary>
    [HttpPost("api/v1/reviews/{reviewSessionId:guid}/discussions")]
    public async Task<IActionResult> SendReviewDiscussion(
        Guid reviewSessionId, [FromBody] SendSprintDiscussionDto dto, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var displayName = User.Identity?.Name ?? "Unknown";

        try
        {
            var message = await _discussionService.SendReviewSessionMessageAsync(
                reviewSessionId, caller.UserId, displayName, dto.Content, ct);
            return Created($"/api/v1/reviews/{reviewSessionId}/discussions", Envelope(message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }
}
