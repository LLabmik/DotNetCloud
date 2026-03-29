using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for planning poker sessions.
/// </summary>
[Route("api/v1")]
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

    /// <summary>Gets all poker sessions for a card.</summary>
    [HttpGet("cards/{cardId:guid}/poker")]
    public async Task<IActionResult> GetCardSessionsAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sessions = await _pokerService.GetCardSessionsAsync(cardId, caller);
            return Ok(Envelope(sessions));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Starts a new planning poker session for a card.</summary>
    [HttpPost("cards/{cardId:guid}/poker")]
    public async Task<IActionResult> StartSessionAsync(Guid cardId, [FromBody] CreatePokerSessionDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.StartSessionAsync(cardId, dto, caller);
            return Created($"/api/v1/poker/{session.Id}", Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a poker session by ID.</summary>
    [HttpGet("poker/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        var session = await _pokerService.GetSessionAsync(sessionId, caller);
        return session is null
            ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, "Poker session not found."))
            : Ok(Envelope(session));
    }

    /// <summary>Submits or updates a vote in a poker session.</summary>
    [HttpPost("poker/{sessionId:guid}/vote")]
    public async Task<IActionResult> SubmitVoteAsync(Guid sessionId, [FromBody] SubmitPokerVoteDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.SubmitVoteAsync(sessionId, dto, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.PokerSessionNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Reveals all votes in a poker session.</summary>
    [HttpPost("poker/{sessionId:guid}/reveal")]
    public async Task<IActionResult> RevealSessionAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.RevealSessionAsync(sessionId, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.PokerSessionNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Accepts an estimate, applies it to the card, and completes the session.</summary>
    [HttpPost("poker/{sessionId:guid}/accept")]
    public async Task<IActionResult> AcceptEstimateAsync(Guid sessionId, [FromBody] AcceptPokerEstimateDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.AcceptEstimateAsync(sessionId, dto, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.PokerSessionNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Starts a new voting round in a poker session (clears current votes, increments round).</summary>
    [HttpPost("poker/{sessionId:guid}/new-round")]
    public async Task<IActionResult> StartNewRoundAsync(Guid sessionId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var session = await _pokerService.StartNewRoundAsync(sessionId, caller);
            return Ok(Envelope(session));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.PokerSessionNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.PokerSessionNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
