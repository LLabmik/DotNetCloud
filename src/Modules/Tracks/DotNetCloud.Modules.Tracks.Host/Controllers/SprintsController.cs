using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for sprint management, sprint items, backlog, and sprint planning.
/// </summary>
[ApiController]
public class SprintsController : TracksControllerBase
{
    private readonly SprintService _sprintService;
    private readonly SprintPlanningService _sprintPlanningService;
    private readonly ILogger<SprintsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintsController"/> class.
    /// </summary>
    public SprintsController(SprintService sprintService, SprintPlanningService sprintPlanningService, ILogger<SprintsController> logger)
    {
        _sprintService = sprintService;
        _sprintPlanningService = sprintPlanningService;
        _logger = logger;
    }

    // ─── Sprint CRUD ────────────────────────────────────────────────────────

    /// <summary>Lists all sprints for an epic.</summary>
    [HttpGet("api/v1/workitems/{epicId:guid}/sprints")]
    public async Task<IActionResult> GetSprintsAsync(Guid epicId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprints = await _sprintService.GetSprintsByEpicAsync(epicId, ct);
            return Ok(Envelope(sprints));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Creates a new sprint under an epic.</summary>
    [HttpPost("api/v1/workitems/{epicId:guid}/sprints")]
    public async Task<IActionResult> CreateSprintAsync(Guid epicId, [FromBody] CreateSprintDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.CreateSprintAsync(epicId, dto, ct);
            return Created($"/api/v1/sprints/{sprint.Id}", Envelope(sprint));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("EpicId")
                ? NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Gets a sprint by ID.</summary>
    [HttpGet("api/v1/sprints/{sprintId:guid}")]
    public async Task<IActionResult> GetSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var sprint = await _sprintService.GetSprintAsync(sprintId, ct);
        return sprint is null
            ? NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, "Sprint not found."))
            : Ok(Envelope(sprint));
    }

    /// <summary>Updates a sprint.</summary>
    [HttpPut("api/v1/sprints/{sprintId:guid}")]
    public async Task<IActionResult> UpdateSprintAsync(Guid sprintId, [FromBody] UpdateSprintDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.UpdateSprintAsync(sprintId, dto, ct);
            return Ok(Envelope(sprint));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Deletes a sprint.</summary>
    [HttpDelete("api/v1/sprints/{sprintId:guid}")]
    public async Task<IActionResult> DeleteSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.DeleteSprintAsync(sprintId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    /// <summary>Starts a sprint (Planning to Active).</summary>
    [HttpPost("api/v1/sprints/{sprintId:guid}/start")]
    public async Task<IActionResult> StartSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.StartSprintAsync(sprintId, ct);
            return Ok(Envelope(sprint));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidSprintTransition, ex.Message));
        }
    }

    /// <summary>Completes a sprint (Active to Completed).</summary>
    [HttpPost("api/v1/sprints/{sprintId:guid}/complete")]
    public async Task<IActionResult> CompleteSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.CompleteSprintAsync(sprintId, ct);
            return Ok(Envelope(sprint));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidSprintTransition, ex.Message));
        }
    }

    // ─── Sprint Items ───────────────────────────────────────────────────────

    /// <summary>Adds a work item to a sprint.</summary>
    [HttpPost("api/v1/sprints/{sprintId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> AddItemToSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.AddItemToSprintAsync(sprintId, itemId, ct);
            return Ok(Envelope(new { added = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("ItemId")
                ? BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Removes a work item from a sprint.</summary>
    [HttpDelete("api/v1/sprints/{sprintId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItemFromSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.RemoveItemFromSprintAsync(sprintId, itemId, ct);
            return Ok(Envelope(new { removed = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    // ─── Backlog ────────────────────────────────────────────────────────────

    /// <summary>Gets unassigned backlog items for an epic.</summary>
    [HttpGet("api/v1/workitems/{epicId:guid}/backlog")]
    public async Task<IActionResult> GetBacklogItemsAsync(Guid epicId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var items = await _sprintService.GetBacklogItemsAsync(epicId, ct);
        return Ok(Envelope(items));
    }

    // ─── Sprint Plan ────────────────────────────────────────────────────────

    /// <summary>Gets the sprint plan overview for an epic.</summary>
    [HttpGet("api/v1/workitems/{epicId:guid}/sprint-plan")]
    public async Task<IActionResult> GetSprintPlanAsync(Guid epicId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var plan = await _sprintPlanningService.GetSprintPlanAsync(epicId, ct);
            return Ok(Envelope(plan));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Creates a sequential sprint plan for an epic.</summary>
    [HttpPost("api/v1/workitems/{epicId:guid}/sprint-plan")]
    public async Task<IActionResult> CreateSprintPlanAsync(Guid epicId, [FromBody] CreateSprintPlanDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprints = await _sprintPlanningService.CreateSprintPlanAsync(epicId, dto, ct);
            return Created($"/api/v1/workitems/{epicId}/sprint-plan", Envelope(sprints));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("EpicId")
                ? NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Adjusts a sprint's duration/dates and cascades to subsequent sprints.</summary>
    [HttpPut("api/v1/sprints/{sprintId:guid}/adjust")]
    public async Task<IActionResult> AdjustSprintDatesAsync(Guid sprintId, [FromBody] AdjustSprintDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintPlanningService.AdjustSprintDatesAsync(sprintId, dto, ct);
            return Ok(Envelope(sprint));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey("DurationWeeks")
                ? BadRequest(ErrorEnvelope(ErrorCodes.InvalidSprintDuration, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }
}
