using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for sprint management.
/// </summary>
[Route("api/v1/boards/{boardId:guid}/sprints")]
public class SprintsController : TracksControllerBase
{
    private readonly SprintService _sprintService;
    private readonly ILogger<SprintsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SprintsController"/> class.
    /// </summary>
    public SprintsController(SprintService sprintService, ILogger<SprintsController> logger)
    {
        _sprintService = sprintService;
        _logger = logger;
    }

    /// <summary>Lists sprints for a board.</summary>
    [HttpGet]
    public async Task<IActionResult> ListSprintsAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprints = await _sprintService.GetSprintsAsync(boardId, caller);
            return Ok(Envelope(sprints));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
    }

    /// <summary>Gets a sprint by ID.</summary>
    [HttpGet("{sprintId:guid}")]
    public async Task<IActionResult> GetSprintAsync(Guid boardId, Guid sprintId)
    {
        var caller = GetAuthenticatedCaller();
        var sprint = await _sprintService.GetSprintAsync(sprintId, caller);
        return sprint is null
            ? NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, "Sprint not found."))
            : Ok(Envelope(sprint));
    }

    /// <summary>Creates a new sprint.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSprintAsync(Guid boardId, [FromBody] CreateSprintDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.CreateSprintAsync(boardId, dto, caller);
            return Created($"/api/v1/boards/{boardId}/sprints/{sprint.Id}", Envelope(sprint));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a sprint.</summary>
    [HttpPut("{sprintId:guid}")]
    public async Task<IActionResult> UpdateSprintAsync(Guid boardId, Guid sprintId, [FromBody] UpdateSprintDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.UpdateSprintAsync(sprintId, dto, caller);
            return Ok(Envelope(sprint));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.SprintNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a sprint.</summary>
    [HttpDelete("{sprintId:guid}")]
    public async Task<IActionResult> DeleteSprintAsync(Guid boardId, Guid sprintId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.DeleteSprintAsync(sprintId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }

    /// <summary>Starts a sprint.</summary>
    [HttpPost("{sprintId:guid}/start")]
    public async Task<IActionResult> StartSprintAsync(Guid boardId, Guid sprintId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.StartSprintAsync(sprintId, caller);
            return Ok(Envelope(sprint));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.SprintNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.ActiveSprintExists))
                return Conflict(ErrorEnvelope(ErrorCodes.ActiveSprintExists, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Completes a sprint.</summary>
    [HttpPost("{sprintId:guid}/complete")]
    public async Task<IActionResult> CompleteSprintAsync(Guid boardId, Guid sprintId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var sprint = await _sprintService.CompleteSprintAsync(sprintId, caller);
            return Ok(Envelope(sprint));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.SprintNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
            if (ex.Errors.ContainsKey(ErrorCodes.InvalidSprintTransition))
                return BadRequest(ErrorEnvelope(ErrorCodes.InvalidSprintTransition, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Sprint Cards ─────────────────────────────────────────────────────

    /// <summary>Adds a card to a sprint.</summary>
    [HttpPost("{sprintId:guid}/cards/{cardId:guid}")]
    public async Task<IActionResult> AddCardToSprintAsync(Guid boardId, Guid sprintId, Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.AddCardToSprintAsync(sprintId, cardId, caller);
            return Ok(Envelope(new { added = true }));
        }
        catch (ValidationException ex)
        {
            if (ex.Errors.ContainsKey(ErrorCodes.SprintNotFound) || ex.Errors.ContainsKey(ErrorCodes.CardNotFound))
                return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a card from a sprint.</summary>
    [HttpDelete("{sprintId:guid}/cards/{cardId:guid}")]
    public async Task<IActionResult> RemoveCardFromSprintAsync(Guid boardId, Guid sprintId, Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _sprintService.RemoveCardFromSprintAsync(sprintId, cardId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.SprintNotFound, ex.Message));
        }
    }
}
