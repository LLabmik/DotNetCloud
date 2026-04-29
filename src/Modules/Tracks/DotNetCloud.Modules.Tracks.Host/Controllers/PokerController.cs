using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for planning poker sessions.
/// </summary>
[ApiController]
public class PokerController : TracksControllerBase
{
    private readonly PokerService _pokerService;
    private readonly ILogger<PokerController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PokerController"/> class.
    /// </summary>
    public PokerController(PokerService pokerService, ILogger<PokerController> logger)
    {
        _pokerService = pokerService;
        _logger = logger;
    }

    /// <summary>Starts a new planning poker session for an item under an epic.</summary>
    [HttpPost("api/v1/workitems/{epicId:guid}/poker")]
    public async Task<IActionResult> StartSessionAsync(Guid epicId, [FromBody] CreatePokerSessionDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.StartSessionAsync(epicId, caller.UserId, dto, ct);
            return Created($"/api/v1/poker/{session.Id}", Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("EpicId") || ex.Errors.ContainsKey("ItemId")
                ? NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Gets a poker session by ID.</summary>
    [HttpGet("api/v1/poker/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var session = await _pokerService.GetSessionAsync(sessionId, ct);
        return session is null
            ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, "Poker session not found."))
            : Ok(Envelope(session));
    }

    /// <summary>Submits or updates a vote in a poker session.</summary>
    [HttpPost("api/v1/poker/{sessionId:guid}/vote")]
    public async Task<IActionResult> SubmitVoteAsync(Guid sessionId, [FromBody] SubmitPokerVoteDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.SubmitVoteAsync(sessionId, caller.UserId, dto, ct);
            return Ok(Envelope(session));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.PokerSessionNotVoting, ex.Message));
        }
    }

    /// <summary>Reveals all votes in a poker session.</summary>
    [HttpPost("api/v1/poker/{sessionId:guid}/reveal")]
    public async Task<IActionResult> RevealVotesAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.RevealVotesAsync(sessionId, ct);
            return Ok(Envelope(session));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.PokerSessionNotVoting, ex.Message));
        }
    }

    /// <summary>Accepts an estimate, applies it to the item, and completes the session.</summary>
    [HttpPost("api/v1/poker/{sessionId:guid}/accept")]
    public async Task<IActionResult> AcceptEstimateAsync(Guid sessionId, [FromBody] AcceptEstimateRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.AcceptEstimateAsync(sessionId, request.Estimate, ct);
            return Ok(Envelope(session));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Gets the current vote status for a poker session (per-user vote status without revealing values).</summary>
    [HttpGet("api/v1/poker/{sessionId:guid}/vote-status")]
    public async Task<IActionResult> GetVoteStatusAsync(Guid sessionId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var statuses = await _pokerService.GetVoteStatusAsync(sessionId, ct);
        return Ok(Envelope(statuses));
    }
}

/// <summary>Request body for accepting a poker estimate.</summary>
public sealed record AcceptEstimateRequest
{
    /// <summary>The estimate value to apply to the item.</summary>
    public required string Estimate { get; init; }
}
