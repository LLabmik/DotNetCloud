using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            if (ex.Message.Contains("Cannot move from"))
                return Conflict(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
            if (ex.Message.Contains("WIP limit"))
                return Conflict(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
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

    // ─── Watchers ──────────────────────────────────────────────────────

    /// <summary>
    /// Gets the list of users watching (subscribed to) a work item.
    /// Watchers get notified when the item changes, even if they're not assigned.
    /// </summary>
    [HttpGet("workitems/{workItemId:guid}/watchers")]
    public async Task<IActionResult> GetWatchersAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var watchers = await _db.WorkItemWatchers
                .Where(w => w.WorkItemId == workItemId)
                .Select(w => new { w.UserId, w.SubscribedAt })
                .ToListAsync(ct);

            return Ok(Envelope(watchers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get watchers for work item {WorkItemId}", workItemId);
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>
    /// Start watching a work item. You'll get notified when it's updated or commented on.
    /// This is like "subscribing" to a ticket — you don't need to be assigned to follow along.
    /// </summary>
    [HttpPost("workitems/{workItemId:guid}/watch")]
    public async Task<IActionResult> WatchAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var exists = await _db.WorkItemWatchers
                .AnyAsync(w => w.WorkItemId == workItemId && w.UserId == caller.UserId, ct);

            if (!exists)
            {
                _db.WorkItemWatchers.Add(new WorkItemWatcher
                {
                    WorkItemId = workItemId,
                    UserId = caller.UserId,
                    SubscribedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            }

            var count = await _db.WorkItemWatchers
                .CountAsync(w => w.WorkItemId == workItemId, ct);

            return Ok(Envelope(new { watching = true, watcherCount = count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to watch work item {WorkItemId}", workItemId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>
    /// Stop watching a work item. You'll no longer get notifications for changes.
    /// </summary>
    [HttpDelete("workitems/{workItemId:guid}/watch")]
    public async Task<IActionResult> UnwatchAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var watcher = await _db.WorkItemWatchers
                .FirstOrDefaultAsync(w => w.WorkItemId == workItemId && w.UserId == caller.UserId, ct);

            if (watcher is not null)
            {
                _db.WorkItemWatchers.Remove(watcher);
                await _db.SaveChangesAsync(ct);
            }

            var count = await _db.WorkItemWatchers
                .CountAsync(w => w.WorkItemId == workItemId, ct);

            return Ok(Envelope(new { watching = false, watcherCount = count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unwatch work item {WorkItemId}", workItemId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── Export ────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all non-deleted work items for a product across all swimlanes.
    /// Supports optional filtering by swimlane, label, and priority.
    /// </summary>
    [HttpGet("products/{productId:guid}/work-items")]
    public async Task<IActionResult> ListProductWorkItemsAsync(
        Guid productId,
        [FromQuery] Guid? swimlaneId = null,
        [FromQuery] Guid? labelId = null,
        [FromQuery] Priority? priority = null,
        CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var query = _db.WorkItems
                .Include(wi => wi.Assignments)
                .Include(wi => wi.WorkItemLabels!)
                    .ThenInclude(wl => wl.Label)
                .Include(wi => wi.SprintItems!)
                    .ThenInclude(si => si.Sprint)
                .Include(wi => wi.Swimlane)
                .Where(wi => wi.ProductId == productId && !wi.IsDeleted);

            if (swimlaneId.HasValue)
                query = query.Where(wi => wi.SwimlaneId == swimlaneId.Value);
            if (labelId.HasValue)
                query = query.Where(wi => wi.WorkItemLabels!.Any(wl => wl.LabelId == labelId.Value));
            if (priority.HasValue)
                query = query.Where(wi => wi.Priority == priority.Value);

            var workItems = await query
                .OrderBy(wi => wi.SwimlaneId)
                .ThenBy(wi => wi.Position)
                .ToListAsync(ct);

            var dtos = workItems.Select(wi => new WorkItemDto
            {
                Id = wi.Id,
                ProductId = wi.ProductId,
                ParentWorkItemId = wi.ParentWorkItemId,
                Type = wi.Type,
                SwimlaneId = wi.SwimlaneId,
                SwimlaneTitle = wi.Swimlane?.Title ?? "",
                ItemNumber = wi.ItemNumber,
                Title = wi.Title ?? "",
                Description = wi.Description,
                Position = wi.Position,
                Priority = wi.Priority,
                DueDate = wi.DueDate,
                StoryPoints = wi.StoryPoints,
                IsArchived = wi.IsArchived,
                CommentCount = wi.Comments?.Count ?? 0,
                AttachmentCount = wi.Attachments?.Count ?? 0,
                Assignments = wi.Assignments?.Select(a => new WorkItemAssignmentDto
                {
                    UserId = a.UserId,
                    DisplayName = a.UserId.ToString(),
                    AssignedAt = a.AssignedAt
                }).ToList() ?? [],
                Labels = wi.WorkItemLabels?.Select(wl => new LabelDto
                {
                    Id = wl.Label!.Id,
                    ProductId = productId,
                    Title = wl.Label.Title,
                    Color = wl.Label.Color ?? "#6b7280",
                    CreatedAt = wl.Label.CreatedAt
                }).ToList() ?? [],
                SprintId = wi.SprintItems?.FirstOrDefault()?.SprintId,
                SprintTitle = wi.SprintItems?.FirstOrDefault()?.Sprint?.Title,
                TotalTrackedMinutes = 0,
                ETag = wi.ETag,
                CreatedAt = wi.CreatedAt,
                UpdatedAt = wi.UpdatedAt
            }).ToList();

            return Ok(Envelope(dtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list work items for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>
    /// Performs a bulk action on multiple work items (archive, delete, move, label, assign, priority, sprint).
    /// </summary>
    [HttpPost("products/{productId:guid}/work-items/bulk")]
    public async Task<IActionResult> BulkWorkItemActionAsync(
        Guid productId, [FromBody] BulkWorkItemActionDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            if (dto.WorkItemIds.Count == 0)
                return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "No work item IDs provided."));

            var items = await _db.WorkItems
                .Where(wi => dto.WorkItemIds.Contains(wi.Id) && wi.ProductId == productId && !wi.IsDeleted)
                .ToListAsync(ct);

            if (items.Count == 0)
                return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, "No matching work items found."));

            switch (dto.Action)
            {
                case "archive":
                    foreach (var item in items) item.IsArchived = true;
                    break;
                case "unarchive":
                    foreach (var item in items) item.IsArchived = false;
                    break;
                case "delete":
                    foreach (var item in items)
                    {
                        item.IsDeleted = true;
                        item.DeletedAt = DateTime.UtcNow;
                    }
                    break;
                case "move" when dto.TargetSwimlaneId.HasValue:
                    var targetSwimlane = await _db.Swimlanes
                        .FirstOrDefaultAsync(s => s.Id == dto.TargetSwimlaneId.Value, ct);
                    if (targetSwimlane is null)
                        return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "Target swimlane not found."));
                    foreach (var item in items) item.SwimlaneId = dto.TargetSwimlaneId.Value;
                    break;
                case "add-label" when dto.LabelId.HasValue:
                    foreach (var item in items)
                    {
                        if (!await _db.WorkItemLabels.AnyAsync(wl => wl.WorkItemId == item.Id && wl.LabelId == dto.LabelId.Value, ct))
                            _db.WorkItemLabels.Add(new WorkItemLabel { WorkItemId = item.Id, LabelId = dto.LabelId.Value });
                    }
                    break;
                case "assign" when dto.AssigneeUserId.HasValue:
                    foreach (var item in items)
                    {
                        if (!await _db.WorkItemAssignments.AnyAsync(a => a.WorkItemId == item.Id && a.UserId == dto.AssigneeUserId.Value, ct))
                            _db.WorkItemAssignments.Add(new WorkItemAssignment { WorkItemId = item.Id, UserId = dto.AssigneeUserId.Value, AssignedAt = DateTime.UtcNow });
                    }
                    break;
                case "set-priority" when dto.Priority.HasValue:
                    foreach (var item in items) item.Priority = dto.Priority.Value;
                    break;
                case "assign-sprint" when dto.SprintId.HasValue:
                    foreach (var item in items)
                    {
                        if (!await _db.SprintItems.AnyAsync(si => si.ItemId == item.Id && si.SprintId == dto.SprintId.Value, ct))
                            _db.SprintItems.Add(new SprintItem { SprintId = dto.SprintId.Value, ItemId = item.Id, AddedAt = DateTime.UtcNow });
                    }
                    break;
                default:
                    return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, $"Unknown or incomplete bulk action: {dto.Action}"));
            }

            await _db.SaveChangesAsync(ct);
            return Ok(Envelope(new { affected = items.Count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform bulk action on work items for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── Export ────────────────────────────────────────────────────────

    /// <summary>
    /// Exports work items for a product as a CSV file.
    /// The CSV can be opened directly in Excel, Google Sheets, or Numbers.
    /// Supports optional filtering by swimlane, label, and priority.
    /// </summary>
    /// <param name="productId">The product whose work items to export.</param>
    /// <param name="swimlaneId">Optional: filter to a specific swimlane/status column.</param>
    /// <param name="labelId">Optional: filter to items with a specific label.</param>
    /// <param name="priority">Optional: filter to items of a specific priority level.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("products/{productId:guid}/work-items/export")]
    public async Task<IActionResult> ExportWorkItemsCsvAsync(
        Guid productId,
        [FromQuery] Guid? swimlaneId = null,
        [FromQuery] Guid? labelId = null,
        [FromQuery] Priority? priority = null,
        CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();

        try
        {
            // Start with all non-deleted work items in the product
            var query = _db.WorkItems
                .Include(wi => wi.Assignments)
                .Include(wi => wi.WorkItemLabels!)
                    .ThenInclude(wl => wl.Label)
                .Include(wi => wi.SprintItems!)
                    .ThenInclude(si => si.Sprint)
                .Include(wi => wi.Swimlane)
                .Where(wi => wi.ProductId == productId && !wi.IsDeleted);

            // Apply optional filters
            if (swimlaneId.HasValue)
                query = query.Where(wi => wi.SwimlaneId == swimlaneId.Value);

            if (labelId.HasValue)
                query = query.Where(wi => wi.WorkItemLabels!.Any(wl => wl.LabelId == labelId.Value));

            if (priority.HasValue)
                query = query.Where(wi => wi.Priority == priority.Value);

            var workItems = await query
                .OrderBy(wi => wi.SwimlaneId)
                .ThenBy(wi => wi.Position)
                .ToListAsync(ct);

            // Map to minimal DTOs for CSV export
            var dtos = workItems.Select(wi => new WorkItemDto
            {
                Id = wi.Id,
                ProductId = wi.ProductId,
                Type = wi.Type,
                SwimlaneId = wi.SwimlaneId,
                SwimlaneTitle = wi.Swimlane?.Title ?? "",
                ItemNumber = wi.ItemNumber,
                Title = wi.Title ?? "",
                Description = wi.Description,
                Position = wi.Position,
                Priority = wi.Priority,
                DueDate = wi.DueDate,
                StoryPoints = wi.StoryPoints,
                IsArchived = wi.IsArchived,
                CommentCount = 0,
                AttachmentCount = 0,
                Assignments = wi.Assignments?.Select(a => new WorkItemAssignmentDto
                {
                    UserId = a.UserId,
                    DisplayName = a.UserId.ToString(),
                    AssignedAt = a.AssignedAt
                }).ToList() ?? [],
                Labels = wi.WorkItemLabels?.Select(wl => new LabelDto
                {
                    Id = wl.Label!.Id,
                    ProductId = productId,
                    Title = wl.Label.Title,
                    Color = wl.Label.Color ?? "#6b7280",
                    CreatedAt = wl.Label.CreatedAt
                }).ToList() ?? [],
                SprintId = wi.SprintItems?.FirstOrDefault()?.SprintId,
                SprintTitle = wi.SprintItems?.FirstOrDefault()?.Sprint?.Title,
                TotalTrackedMinutes = 0,
                ETag = wi.ETag,
                CreatedAt = wi.CreatedAt,
                UpdatedAt = wi.UpdatedAt
            }).ToList();

            var csvBytes = WorkItemCsvExporter.ExportToCsv(dtos);
            var productName = await _db.Products
                .Where(p => p.Id == productId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct) ?? "work-items";

            var fileName = $"{SanitizeFileName(productName)}-export-{DateTime.UtcNow:yyyy-MM-dd}.csv";
            return File(csvBytes, "text/csv; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export work items for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    // ─── CSV Import ────────────────────────────────────────────────────

    /// <summary>
    /// Imports work items from a CSV file. Supports column mapping, validation,
    /// and batch creation. Use dry-run mode to validate without creating.
    /// </summary>
    [HttpPost("products/{productId:guid}/work-items/import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> ImportWorkItemsCsvAsync(
        Guid productId,
        IFormFile file,
        [FromQuery] Guid? swimlaneId = null,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();

        if (file is null || file.Length == 0)
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "No file uploaded."));

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, "Only CSV files are supported."));

        try
        {
            using var stream = file.OpenReadStream();

            var csvService = HttpContext.RequestServices.GetRequiredService<CsvImportService>();

            // Parse the CSV
            var parseResult = await csvService.ParseCsvAsync(stream, ct);

            // Build auto-mapping from headers
            var mapping = AutoMapColumns(parseResult.Headers);

            stream.Position = 0;

            if (dryRun)
            {
                var validation = await csvService.ValidateCsvAsync(productId, stream, mapping, ct);
                return Ok(Envelope(new
                {
                    dryRun = true,
                    headers = parseResult.Headers,
                    delimiter = parseResult.Delimiter,
                    previewRows = parseResult.PreviewRows,
                    totalRows = parseResult.TotalRows,
                    validRowCount = validation.ValidRowCount,
                    errors = validation.Errors
                }));
            }

            var result = await csvService.ImportCsvAsync(
                productId,
                swimlaneId ?? Guid.Empty,
                caller.UserId,
                stream,
                mapping,
                skipDuplicates: false,
                ct);

            return Ok(Envelope(new
            {
                created = result.Created,
                skipped = result.Skipped,
                failed = result.Failed,
                batchCount = result.BatchCount,
                errors = result.Errors
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import CSV for product {ProductId}", productId);
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Removes characters that are invalid in file names.</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized.Trim();
    }

    /// <summary>
    /// Automatically maps CSV column headers to known work item field names.
    /// </summary>
    private static CsvColumnMapping AutoMapColumns(List<string> headers)
    {
        var mapping = new CsvColumnMapping();

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i].ToLowerInvariant().Trim();

            if (header.Contains("title") || header.Contains("name") || header.Contains("summary"))
                mapping.TitleColumn = i;
            else if (header.Contains("desc"))
                mapping.DescriptionColumn = i;
            else if (header.Contains("prior"))
                mapping.PriorityColumn = i;
            else if (header.Contains("type") || header.Contains("kind"))
                mapping.TypeColumn = i;
            else if (header.Contains("point") || header.Contains("story") || header.Contains("estimate") || header.Contains("effort"))
                mapping.StoryPointsColumn = i;
            else if (header.Contains("assign") || header.Contains("email") || header.Contains("owner"))
                mapping.AssigneeEmailColumn = i;
            else if (header.Contains("due") || header.Contains("date") || header.Contains("deadline"))
                mapping.DueDateColumn = i;
            else if (header.Contains("label") || header.Contains("tag"))
                mapping.LabelsColumn = i;
        }

        return mapping;
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
