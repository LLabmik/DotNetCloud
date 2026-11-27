using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for product milestones.
/// </summary>
[ApiController]
public class MilestonesController : TracksControllerBase
{
    private readonly MilestoneService _milestoneService;
    private readonly ILogger<MilestonesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MilestonesController"/> class.
    /// </summary>
    public MilestonesController(MilestoneService milestoneService, ILogger<MilestonesController> logger)
    {
        _milestoneService = milestoneService;
        _logger = logger;
    }

    /// <summary>Lists all milestones for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/milestones")]
    public async Task<IActionResult> GetMilestonesAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var milestones = await _milestoneService.GetMilestonesAsync(productId, ct);
            return Ok(Envelope(milestones));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing milestones for product {ProductId}", productId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to list milestones."));
        }
    }

    /// <summary>Gets a single milestone by ID.</summary>
    [HttpGet("api/v1/milestones/{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestoneAsync(Guid milestoneId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var milestone = await _milestoneService.GetMilestoneAsync(milestoneId, ct);
        if (milestone is null)
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, $"Milestone {milestoneId} not found."));
        return Ok(Envelope(milestone));
    }

    /// <summary>Creates a new milestone on a product.</summary>
    [HttpPost("api/v1/products/{productId:guid}/milestones")]
    public async Task<IActionResult> CreateMilestoneAsync(Guid productId, [FromBody] CreateMilestoneDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var milestone = await _milestoneService.CreateMilestoneAsync(productId, dto, ct);
            return Created($"/api/v1/milestones/{milestone.Id}", Envelope(milestone));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Updates a milestone.</summary>
    [HttpPut("api/v1/milestones/{milestoneId:guid}")]
    public async Task<IActionResult> UpdateMilestoneAsync(Guid milestoneId, [FromBody] UpdateMilestoneDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var milestone = await _milestoneService.UpdateMilestoneAsync(milestoneId, dto, ct);
            return Ok(Envelope(milestone));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Sets the status of a milestone (Upcoming/Active/Completed).</summary>
    [HttpPut("api/v1/milestones/{milestoneId:guid}/status")]
    public async Task<IActionResult> SetStatusAsync(Guid milestoneId, [FromBody] SetMilestoneStatusDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var milestone = await _milestoneService.SetStatusAsync(milestoneId, dto, ct);
            return Ok(Envelope(milestone));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Deletes a milestone (unlinks any assigned work items).</summary>
    [HttpDelete("api/v1/milestones/{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestoneAsync(Guid milestoneId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _milestoneService.DeleteMilestoneAsync(milestoneId, ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }
}
