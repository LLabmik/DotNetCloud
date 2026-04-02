using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for live review sessions with integrated planning poker.
/// </summary>
[Route("api/v1")]
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

    /// <summary>Starts a new review session on a board. Requires Admin role and Team-mode board.</summary>
    [HttpPost("boards/{boardId:guid}/review-session")]
    public async Task<IActionResult> StartSessionAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.StartSessionAsync(boardId, caller);
            return Created($"/api/v1/review-sessions/{session.Id}", Envelope(session));
        }
        catch (ValidationException ex)
        {
            if (IsBoardNotFound(ex))
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionAlreadyActive))
                return Conflict(ErrorEnvelope(ErrorCodes.ReviewSessionAlreadyActive, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets the active review session for a board, if any.</summary>
    [HttpGet("boards/{boardId:guid}/review-session")]
    public async Task<IActionResult> GetActiveSessionForBoardAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.GetActiveSessionForBoardAsync(boardId, caller);
            return session is null
                ? NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, "No active review session for this board."))
                : Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a review session by ID.</summary>
    [HttpGet("review-sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        var session = await _reviewSessionService.GetSessionStateAsync(sessionId, caller);
        return session is null
            ? NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, "Review session not found."))
            : Ok(Envelope(session));
    }

    /// <summary>Joins an existing review session.</summary>
    [HttpPost("review-sessions/{sessionId:guid}/join")]
    public async Task<IActionResult> JoinSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.JoinSessionAsync(sessionId, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionEnded))
                return BadRequest(ErrorEnvelope(ErrorCodes.ReviewSessionEnded, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Leaves a review session (marks participant as disconnected).</summary>
    [HttpPost("review-sessions/{sessionId:guid}/leave")]
    public async Task<IActionResult> LeaveSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        await _reviewSessionService.LeaveSessionAsync(sessionId, caller);
        return Ok(Envelope(new { left = true }));
    }

    /// <summary>Sets the current card being reviewed. Host only.</summary>
    [HttpPut("review-sessions/{sessionId:guid}/current-card")]
    public async Task<IActionResult> SetCurrentCardAsync(Guid sessionId, [FromBody] SetReviewCurrentCardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.SetCurrentCardAsync(sessionId, dto.CardId, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost))
                return StatusCode(403, ErrorEnvelope(ErrorCodes.ReviewSessionNotHost, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Starts a planning poker session for the current card in the review. Host only.</summary>
    [HttpPost("review-sessions/{sessionId:guid}/poker")]
    public async Task<IActionResult> StartPokerAsync(Guid sessionId, [FromBody] StartReviewPokerDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _reviewSessionService.StartPokerForCurrentCardAsync(sessionId, dto, caller);
            return Created($"/api/v1/review-sessions/{sessionId}", Envelope(session));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost))
                return StatusCode(403, ErrorEnvelope(ErrorCodes.ReviewSessionNotHost, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewPokerStillActive))
                return Conflict(ErrorEnvelope(ErrorCodes.ReviewPokerStillActive, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Ends a review session. Host only.</summary>
    [HttpPost("review-sessions/{sessionId:guid}/end")]
    public async Task<IActionResult> EndSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _reviewSessionService.EndSessionAsync(sessionId, caller);
            return Ok(Envelope(new { ended = true }));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.ReviewSessionNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost))
                return StatusCode(403, ErrorEnvelope(ErrorCodes.ReviewSessionNotHost, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
