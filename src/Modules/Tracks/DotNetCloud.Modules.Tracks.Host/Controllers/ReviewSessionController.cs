using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for live review sessions with integrated planning poker.
/// </summary>
[ApiController]
public class ReviewSessionController : TracksControllerBase
{
    private readonly ReviewSessionService _reviewSessionService;
    private readonly ILogger<ReviewSessionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewSessionController"/> class.
    /// </summary>
    public ReviewSessionController(ReviewSessionService reviewSessionService, ILogger<ReviewSessionController> logger)
    {
        _reviewSessionService = reviewSessionService;
        _logger = logger;
    }

    /// <summary>Starts a new review session for an epic.</summary>
    [HttpPost("api/v1/workitems/{epicId:guid}/reviews")]
    public async Task<IActionResult> StartReviewSessionAsync(Guid epicId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.StartReviewSessionAsync(epicId, caller.UserId, ct);
            return Created($"/api/v1/reviews/{session.Id}", Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("EpicId")
                ? NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Gets a review session by ID.</summary>
    [HttpGet("api/v1/reviews/{sessionId:guid}")]
    public async Task<IActionResult> GetReviewSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var session = await _reviewSessionService.GetReviewSessionAsync(sessionId, ct);
        return session is null
            ? NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, "Review session not found."))
            : Ok(Envelope(session));
    }

    /// <summary>Gets participants for a review session.</summary>
    [HttpGet("api/v1/reviews/{sessionId:guid}/participants")]
    public async Task<IActionResult> GetParticipantsAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var participants = await _reviewSessionService.GetParticipantsAsync(sessionId, ct);
        return Ok(Envelope(participants));
    }

    /// <summary>Joins an existing review session.</summary>
    [HttpPost("api/v1/reviews/{sessionId:guid}/join")]
    public async Task<IActionResult> JoinSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var participant = await _reviewSessionService.JoinSessionAsync(sessionId, caller.UserId, ct);
            return Ok(Envelope(participant));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ReviewSessionEnded, ex.Message));
        }
    }

    /// <summary>Leaves a review session (marks participant as disconnected).</summary>
    [HttpPost("api/v1/reviews/{sessionId:guid}/leave")]
    public async Task<IActionResult> LeaveSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _reviewSessionService.LeaveSessionAsync(sessionId, caller.UserId, ct);
            return Ok(Envelope(new { left = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
        }
    }

    /// <summary>Sets the current item being reviewed. Intended for the session host.</summary>
    [HttpPut("api/v1/reviews/{sessionId:guid}/current-item")]
    public async Task<IActionResult> SetCurrentItemAsync(Guid sessionId, [FromBody] SetCurrentItemRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.SetCurrentItemAsync(sessionId, request.ItemId, ct);
            return Ok(Envelope(session));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("ItemId")
                ? NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ReviewSessionEnded, ex.Message));
        }
    }

    /// <summary>Ends a review session.</summary>
    [HttpPost("api/v1/reviews/{sessionId:guid}/end")]
    public async Task<IActionResult> EndSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _reviewSessionService.EndSessionAsync(sessionId, ct);
            return Ok(Envelope(new { ended = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ReviewSessionEnded, ex.Message));
        }
    }
}

/// <summary>Request body for setting the current review item.</summary>
public sealed record SetCurrentItemRequest
{
    /// <summary>The work item ID to set as the current review item.</summary>
    public required Guid ItemId { get; init; }
}
