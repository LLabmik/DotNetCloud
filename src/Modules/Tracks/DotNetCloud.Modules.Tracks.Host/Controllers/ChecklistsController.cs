using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for checklists and checklist items on work items.
/// </summary>
[ApiController]
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

    /// <summary>Lists all checklists on a work item.</summary>
    [HttpGet("api/v1/workitems/{itemId:guid}/checklists")]
    public async Task<IActionResult> GetChecklistsAsync(Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var checklists = await _checklistService.GetChecklistsByItemAsync(itemId, ct);
        return Ok(Envelope(checklists));
    }

    /// <summary>Creates a new checklist on a work item.</summary>
    [HttpPost("api/v1/workitems/{itemId:guid}/checklists")]
    public async Task<IActionResult> CreateChecklistAsync(Guid itemId, [FromBody] CreateChecklistDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var checklist = await _checklistService.CreateChecklistAsync(itemId, dto, ct);
            return Created($"/api/v1/workitems/{itemId}/checklists", Envelope(checklist));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Deletes a checklist and all its items.</summary>
    [HttpDelete("api/v1/workitems/{itemId:guid}/checklists/{checklistId:guid}")]
    public async Task<IActionResult> DeleteChecklistAsync(Guid itemId, Guid checklistId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        await _checklistService.DeleteChecklistAsync(checklistId, ct);
        return Ok(Envelope(new { deleted = true }));
    }

    /// <summary>Adds an item to a checklist.</summary>
    [HttpPost("api/v1/workitems/{itemId:guid}/checklists/{checklistId:guid}/items")]
    public async Task<IActionResult> AddChecklistItemAsync(Guid itemId, Guid checklistId, [FromBody] AddChecklistItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var checklistItem = await _checklistService.AddChecklistItemAsync(checklistId, dto, ct);
            return Created($"/api/v1/workitems/{itemId}/checklists/{checklistId}/items", Envelope(checklistItem));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Toggles a checklist item's completion state.</summary>
    [HttpPut("api/v1/workitems/{itemId:guid}/checklists/{checklistId:guid}/items/{checklistItemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _checklistService.ToggleChecklistItemAsync(checklistItemId, ct);
            return Ok(Envelope(new { toggled = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.ChecklistNotFound, ex.Message));
        }
    }

    /// <summary>Deletes a checklist item.</summary>
    [HttpDelete("api/v1/workitems/{itemId:guid}/checklists/{checklistId:guid}/items/{checklistItemId:guid}")]
    public async Task<IActionResult> DeleteChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        await _checklistService.DeleteChecklistItemAsync(checklistItemId, ct);
        return Ok(Envelope(new { deleted = true }));
    }
}
