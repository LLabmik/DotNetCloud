using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card management, assignments, labels, and movement.
/// </summary>
[Route("api/v1")]
public class CardsController : TracksControllerBase
{
    private readonly CardService _cardService;
    private readonly LabelService _labelService;
    private readonly ActivityService _activityService;
    private readonly ILogger<CardsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardsController"/> class.
    /// </summary>
    public CardsController(
        CardService cardService,
        LabelService labelService,
        ActivityService activityService,
        ILogger<CardsController> logger)
    {
        _cardService = cardService;
        _labelService = labelService;
        _activityService = activityService;
        _logger = logger;
    }

    // ─── Card CRUD ────────────────────────────────────────────────────────

    /// <summary>Lists cards in a swimlane, optionally filtered by sprint.</summary>
    [HttpGet("swimlanes/{swimlaneId:guid}/cards")]
    public async Task<IActionResult> ListCardsAsync(
        Guid swimlaneId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] Guid? sprintId = null)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var cards = await _cardService.ListCardsAsync(swimlaneId, caller, includeArchived, sprintId);
            return Ok(Envelope(cards));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
    }

    /// <summary>Gets a card by ID.</summary>
    [HttpGet("cards/{cardId:guid}")]
    public async Task<IActionResult> GetCardAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        var card = await _cardService.GetCardAsync(cardId, caller);
        return card is null
            ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, "Card not found."))
            : Ok(Envelope(card));
    }

    /// <summary>Gets a card by its human-readable card number.</summary>
    [HttpGet("cards/by-number/{cardNumber:int}")]
    public async Task<IActionResult> GetCardByNumberAsync(int cardNumber)
    {
        var caller = GetAuthenticatedCaller();
        var card = await _cardService.GetCardByNumberAsync(cardNumber, caller);
        return card is null
            ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, "Card not found."))
            : Ok(Envelope(card));
    }

    /// <summary>Creates a new card in a swimlane.</summary>
    [HttpPost("swimlanes/{swimlaneId:guid}/cards")]
    public async Task<IActionResult> CreateCardAsync(Guid swimlaneId, [FromBody] CreateCardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var card = await _cardService.CreateCardAsync(swimlaneId, dto, caller);
            return Created($"/api/v1/cards/{card.Id}", Envelope(card));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardSwimlaneNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a card.</summary>
    [HttpPut("cards/{cardId:guid}")]
    public async Task<IActionResult> UpdateCardAsync(Guid cardId, [FromBody] UpdateCardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var card = await _cardService.UpdateCardAsync(cardId, dto, caller);
            return Ok(Envelope(card));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a card.</summary>
    [HttpDelete("cards/{cardId:guid}")]
    public async Task<IActionResult> DeleteCardAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _cardService.DeleteCardAsync(cardId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Moves a card to another swimlane at a given position.</summary>
    [HttpPut("cards/{cardId:guid}/move")]
    public async Task<IActionResult> MoveCardAsync(Guid cardId, [FromBody] MoveCardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var card = await _cardService.MoveCardAsync(cardId, dto, caller);
            return Ok(Envelope(card));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound) || ex.Errors.ContainsKey(ErrorCodes.BoardSwimlaneNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Card Assignments ─────────────────────────────────────────────────

    /// <summary>Assigns a user to a card.</summary>
    [HttpPost("cards/{cardId:guid}/assign")]
    public async Task<IActionResult> AssignUserAsync(Guid cardId, [FromBody] CardAssignRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _cardService.AssignUserAsync(cardId, request.UserId, caller);
            return Ok(Envelope(new { assigned = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Unassigns a user from a card.</summary>
    [HttpDelete("cards/{cardId:guid}/assign/{userId:guid}")]
    public async Task<IActionResult> UnassignUserAsync(Guid cardId, Guid userId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _cardService.UnassignUserAsync(cardId, userId, caller);
            return Ok(Envelope(new { unassigned = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Card Labels ──────────────────────────────────────────────────────

    /// <summary>Adds a label to a card.</summary>
    [HttpPost("cards/{cardId:guid}/labels")]
    public async Task<IActionResult> AddLabelToCardAsync(Guid cardId, [FromBody] CardLabelRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _labelService.AddLabelToCardAsync(cardId, request.LabelId, caller);
            return Ok(Envelope(new { added = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound) || ex.Errors.ContainsKey(ErrorCodes.LabelNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a label from a card.</summary>
    [HttpDelete("cards/{cardId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> RemoveLabelFromCardAsync(Guid cardId, Guid labelId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _labelService.RemoveLabelFromCardAsync(cardId, labelId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Card Activity ────────────────────────────────────────────────────

    /// <summary>Gets the activity feed for a card.</summary>
    [HttpGet("cards/{cardId:guid}/activity")]
    public async Task<IActionResult> GetCardActivityAsync(
        Guid cardId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var card = await _cardService.GetCardAsync(cardId, caller);
        if (card is null)
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, "Card not found."));

        var activity = await _activityService.GetCardActivityAsync(cardId, skip, take);
        return Ok(Envelope(activity));
    }
}

/// <summary>Request body for assigning a user to a card.</summary>
public sealed record CardAssignRequest
{
    /// <summary>The user ID to assign.</summary>
    public required Guid UserId { get; init; }
}

/// <summary>Request body for adding a label to a card.</summary>
public sealed record CardLabelRequest
{
    /// <summary>The label ID to add.</summary>
    public required Guid LabelId { get; init; }
}
