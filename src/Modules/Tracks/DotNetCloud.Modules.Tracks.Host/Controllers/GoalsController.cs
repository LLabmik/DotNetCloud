using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for goals and key results (OKRs).
/// </summary>
[ApiController]
public class GoalsController : TracksControllerBase
{
    private readonly GoalService _goalService;
    private readonly ILogger<GoalsController> _logger;

    public GoalsController(GoalService goalService, ILogger<GoalsController> logger)
    {
        _goalService = goalService;
        _logger = logger;
    }

    /// <summary>Lists all goals for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/goals")]
    public async Task<IActionResult> ListAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var goals = await _goalService.ListAsync(productId, ct);
        return Ok(Envelope(goals));
    }

    /// <summary>Gets a single goal.</summary>
    [HttpGet("api/v1/goals/{goalId:guid}")]
    public async Task<IActionResult> GetAsync(Guid goalId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var goal = await _goalService.GetAsync(goalId, ct);
        return goal is null ? NotFound() : Ok(Envelope(goal));
    }

    /// <summary>Creates a new goal.</summary>
    [HttpPost("api/v1/products/{productId:guid}/goals")]
    public async Task<IActionResult> CreateAsync(Guid productId, [FromBody] CreateGoalDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var goal = await _goalService.CreateAsync(productId, dto, caller.UserId, ct);
            return CreatedAtAction(nameof(GetAsync), new { goalId = goal.Id }, Envelope(goal));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.BadRequest, ex.Message));
        }
    }

    /// <summary>Updates a goal.</summary>
    [HttpPut("api/v1/goals/{goalId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid goalId, [FromBody] UpdateGoalDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var goal = await _goalService.UpdateAsync(goalId, dto, ct);
        return goal is null ? NotFound() : Ok(Envelope(goal));
    }

    /// <summary>Deletes a goal.</summary>
    [HttpDelete("api/v1/goals/{goalId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid goalId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var deleted = await _goalService.DeleteAsync(goalId, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Links a work item to a goal.</summary>
    [HttpPost("api/v1/goals/{goalId:guid}/work-items")]
    public async Task<IActionResult> LinkWorkItemAsync(Guid goalId, [FromBody] LinkGoalWorkItemDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var linked = await _goalService.LinkWorkItemAsync(goalId, dto.WorkItemId, ct);
        if (!linked) return NotFound();
        return Ok(Envelope(new { linked = true }));
    }

    /// <summary>Unlinks a work item from a goal.</summary>
    [HttpDelete("api/v1/goals/{goalId:guid}/work-items/{workItemId:guid}")]
    public async Task<IActionResult> UnlinkWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var unlinked = await _goalService.UnlinkWorkItemAsync(goalId, workItemId, ct);
        return unlinked ? NoContent() : NotFound();
    }
}
