using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card checklists and checklist items.
/// </summary>
[Route("api/v1/cards/{cardId:guid}/checklists")]
public class ChecklistsController : TracksControllerBase
{
    private readonly ChecklistService _checklistService;
    private readonly ILogger<ChecklistsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecklistsController"/> class.
    /// </summary>
    public ChecklistsController(ChecklistService checklistService, ILogger<ChecklistsController> logger)
    {
        _checklistService = checklistService;
        _logger = logger;
    }

    /// <summary>Lists checklists on a card.</summary>
    [HttpGet]
    public async Task<IActionResult> ListChecklistsAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var checklists = await _checklistService.GetChecklistsAsync(cardId, caller);
            return Ok(Envelope(checklists));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a checklist on a card.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateChecklistAsync(Guid cardId, [FromBody] CreateChecklistRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var checklist = await _checklistService.CreateChecklistAsync(cardId, request.Title, caller);
            return Created($"/api/v1/cards/{cardId}/checklists", Envelope(checklist));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a checklist.</summary>
    [HttpDelete("{checklistId:guid}")]
    public async Task<IActionResult> DeleteChecklistAsync(Guid cardId, Guid checklistId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _checklistService.DeleteChecklistAsync(checklistId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ChecklistNotFound, ex.Message));
        }
    }

    // ─── Checklist Items ──────────────────────────────────────────────────

    /// <summary>Adds an item to a checklist.</summary>
    [HttpPost("{checklistId:guid}/items")]
    public async Task<IActionResult> AddItemAsync(Guid cardId, Guid checklistId, [FromBody] CreateChecklistItemRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _checklistService.AddItemAsync(checklistId, request.Title, caller);
            return Created($"/api/v1/cards/{cardId}/checklists/{checklistId}/items", Envelope(item));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.ChecklistNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.ChecklistNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Toggles a checklist item's completion state.</summary>
    [HttpPut("{checklistId:guid}/items/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleItemAsync(Guid cardId, Guid checklistId, Guid itemId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _checklistService.ToggleItemAsync(itemId, caller);
            return Ok(Envelope(item));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ChecklistNotFound, ex.Message));
        }
    }

    /// <summary>Deletes a checklist item.</summary>
    [HttpDelete("{checklistId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItemAsync(Guid cardId, Guid checklistId, Guid itemId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _checklistService.DeleteItemAsync(itemId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ChecklistNotFound, ex.Message));
        }
    }
}

/// <summary>Request body for creating a checklist.</summary>
public sealed record CreateChecklistRequest
{
    /// <summary>The checklist title.</summary>
    public required string Title { get; init; }
}

/// <summary>Request body for creating a checklist item.</summary>
public sealed record CreateChecklistItemRequest
{
    /// <summary>The item title.</summary>
    public required string Title { get; init; }
}
