using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for work item CRUD, hierarchy, assignments, and labels.
/// </summary>
[Route("api/v1")]
public class WorkItemsController : TracksControllerBase
{
    private readonly WorkItemService _workItemService;
    private readonly TracksDbContext _db;
    private readonly ILogger<WorkItemsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemsController"/> class.
    /// </summary>
    public WorkItemsController(WorkItemService workItemService, TracksDbContext db, ILogger<WorkItemsController> logger)
    {
        _workItemService = workItemService;
        _db = db;
        _logger = logger;
    }

    // ─── Work Item CRUD ────────────────────────────────────────────────

    /// <summary>Lists all work items in a swimlane.</summary>
    [HttpGet("swimlanes/{swimlaneId:guid}/items")]
    public async Task<IActionResult> GetWorkItemsBySwimlaneAsync(Guid swimlaneId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var items = await _workItemService.GetWorkItemsBySwimlaneAsync(swimlaneId, ct);
            return Ok(Envelope(items));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list work items for swimlane {SwimlaneId}", swimlaneId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Creates a new Epic in a swimlane.</summary>
    [HttpPost("swimlanes/{swimlaneId:guid}/epics")]
    public async Task<IActionResult> CreateEpicAsync(Guid swimlaneId, [FromBody] CreateWorkItemDto dto, CancellationToken ct)
    {
        return await CreateWorkItemInSwimlaneAsync(swimlaneId, WorkItemType.Epic, dto, ct);
    }

    /// <summary>Creates a new Feature in a swimlane.</summary>
    [HttpPost("swimlanes/{swimlaneId:guid}/features")]
    public async Task<IActionResult> CreateFeatureAsync(Guid swimlaneId, [FromBody] CreateWorkItemDto dto, CancellationToken ct)
    {
        return await CreateWorkItemInSwimlaneAsync(swimlaneId, WorkItemType.Feature, dto, ct);
    }

    /// <summary>Creates a new Item in a swimlane.</summary>
    [HttpPost("swimlanes/{swimlaneId:guid}/items")]
    public async Task<IActionResult> CreateItemAsync(Guid swimlaneId, [FromBody] CreateWorkItemDto dto, CancellationToken ct)
    {
        return await CreateWorkItemInSwimlaneAsync(swimlaneId, WorkItemType.Item, dto, ct);
    }

    /// <summary>Creates a new SubItem under a parent work item.</summary>
    [HttpPost("workitems/{parentId:guid}/subitems")]
    public async Task<IActionResult> CreateSubItemAsync(Guid parentId, [FromBody] CreateWorkItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var parent = await _db.WorkItems
                .FirstOrDefaultAsync(wi => wi.Id == parentId && !wi.IsDeleted, ct);

            if (parent is null)
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, "Parent work item not found."));

            var item = await _workItemService.CreateWorkItemAsync(
                parent.ProductId, Guid.Empty, WorkItemType.SubItem, caller.UserId, dto, ct);

            return Created($"/api/v1/workitems/{item.Id}", Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subitem under parent {ParentId}", parentId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Gets a work item by ID.</summary>
    [HttpGet("workitems/{workItemId:guid}")]
    public async Task<IActionResult> GetWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _workItemService.GetWorkItemAsync(workItemId, ct);
            return Ok(Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get work item {WorkItemId}", workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Looks up a work item by its product-scoped item number.</summary>
    [HttpGet("workitems/by-number/{productId:guid}/{itemNumber:int}")]
    public async Task<IActionResult> GetWorkItemByNumberAsync(Guid productId, int itemNumber, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _workItemService.GetWorkItemByNumberAsync(productId, itemNumber, ct);
            return Ok(Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get work item by number {ItemNumber} in product {ProductId}", itemNumber, productId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Updates a work item.</summary>
    [HttpPut("workitems/{workItemId:guid}")]
    public async Task<IActionResult> UpdateWorkItemAsync(Guid workItemId, [FromBody] UpdateWorkItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _workItemService.UpdateWorkItemAsync(workItemId, dto, ct);
            return Ok(Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update work item {WorkItemId}", workItemId);
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            if (ex.Message.Contains("modified by another"))
                return Conflict(ErrorEnvelope(ErrorCodes.ConcurrencyConflict, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Soft-deletes a work item.</summary>
    [HttpDelete("workitems/{workItemId:guid}")]
    public async Task<IActionResult> DeleteWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _workItemService.DeleteWorkItemAsync(workItemId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete work item {WorkItemId}", workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    // ─── Move ──────────────────────────────────────────────────────────

    /// <summary>Moves a work item to a different swimlane and/or position.</summary>
    [HttpPut("workitems/{workItemId:guid}/move")]
    public async Task<IActionResult> MoveWorkItemAsync(Guid workItemId, [FromBody] MoveWorkItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _workItemService.MoveWorkItemAsync(workItemId, dto, ct);
            return Ok(Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move work item {WorkItemId}", workItemId);
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── Children ──────────────────────────────────────────────────────

    /// <summary>Gets the direct child work items of a parent work item.</summary>
    [HttpGet("workitems/{workItemId:guid}/children")]
    public async Task<IActionResult> GetChildWorkItemsAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var children = await _workItemService.GetChildWorkItemsAsync(workItemId, ct);
            return Ok(Envelope(children));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get children for work item {WorkItemId}", workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    // ─── Assignments ───────────────────────────────────────────────────

    /// <summary>Gets all user assignments for a work item.</summary>
    [HttpGet("workitems/{workItemId:guid}/assignments")]
    public async Task<IActionResult> GetAssignmentsAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var item = await _workItemService.GetWorkItemAsync(workItemId, ct);
            return Ok(Envelope(item.Assignments));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assignments for work item {WorkItemId}", workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Assigns a user to a work item.</summary>
    [HttpPost("workitems/{workItemId:guid}/assignments")]
    public async Task<IActionResult> AssignUserAsync(
        Guid workItemId, [FromBody] AssignUserRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _workItemService.AssignUserAsync(workItemId, request.UserId, ct);
            return Ok(Envelope(new { assigned = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign user {UserId} to work item {WorkItemId}", request.UserId, workItemId);
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Removes a user assignment from a work item.</summary>
    [HttpDelete("workitems/{workItemId:guid}/assignments/{userId:guid}")]
    public async Task<IActionResult> RemoveAssignmentAsync(Guid workItemId, Guid userId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _workItemService.RemoveAssignmentAsync(workItemId, userId, ct);
            return Ok(Envelope(new { removed = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove assignment of user {UserId} from work item {WorkItemId}", userId, workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    // ─── Labels ────────────────────────────────────────────────────────

    /// <summary>Adds a label to a work item.</summary>
    [HttpPost("workitems/{workItemId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> AddLabelAsync(Guid workItemId, Guid labelId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _workItemService.AddLabelAsync(workItemId, labelId, ct);
            return Ok(Envelope(new { added = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add label {LabelId} to work item {WorkItemId}", labelId, workItemId);
            if (ex.Message.Contains("not found"))
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Removes a label from a work item.</summary>
    [HttpDelete("workitems/{workItemId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> RemoveLabelAsync(Guid workItemId, Guid labelId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _workItemService.RemoveLabelAsync(workItemId, labelId, ct);
            return Ok(Envelope(new { removed = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove label {LabelId} from work item {WorkItemId}", labelId, workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the product ID from a swimlane and creates a work item of the specified type.
    /// </summary>
    private async Task<IActionResult> CreateWorkItemInSwimlaneAsync(
        Guid swimlaneId, WorkItemType type, CreateWorkItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _db.Swimlanes
                .FirstOrDefaultAsync(s => s.Id == swimlaneId && !s.IsArchived, ct);

            if (swimlane is null)
                return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found."));

            if (swimlane.ContainerType != SwimlaneContainerType.Product)
                return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "Work items can only be created in product-level swimlanes."));

            var item = await _workItemService.CreateWorkItemAsync(
                swimlane.ContainerId, swimlaneId, type, caller.UserId, dto, ct);

            return Created($"/api/v1/workitems/{item.Id}", Envelope(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {Type} in swimlane {SwimlaneId}", type, swimlaneId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }
}

// ─── Request DTOs (Controller-level, not shared) ──────────────────────────

/// <summary>Request body for assigning a user to a work item.</summary>
public sealed record AssignUserRequest
{
    /// <summary>The user ID to assign.</summary>
    public required Guid UserId { get; init; }
}
