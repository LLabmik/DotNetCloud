using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for recurring work item rules.
/// </summary>
[ApiController]
public class RecurringRulesController : TracksControllerBase
{
    private readonly RecurringWorkItemService _recurringService;
    private readonly ILogger<RecurringRulesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringRulesController"/> class.
    /// </summary>
    public RecurringRulesController(RecurringWorkItemService recurringService, ILogger<RecurringRulesController> logger)
    {
        _recurringService = recurringService;
        _logger = logger;
    }

    /// <summary>Lists all recurring rules for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/recurring-rules")]
    public async Task<IActionResult> GetRulesAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var rules = await _recurringService.GetRulesAsync(productId, ct);
            return Ok(Envelope(rules));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing recurring rules for product {ProductId}", productId);
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to list recurring rules."));
        }
    }

    /// <summary>Gets a single recurring rule by ID.</summary>
    [HttpGet("api/v1/recurring-rules/{ruleId:guid}")]
    public async Task<IActionResult> GetRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var rule = await _recurringService.GetRuleAsync(ruleId, ct);
        if (rule is null)
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, $"Recurring rule {ruleId} not found."));
        return Ok(Envelope(rule));
    }

    /// <summary>Creates a new recurring rule on a product.</summary>
    [HttpPost("api/v1/products/{productId:guid}/recurring-rules")]
    public async Task<IActionResult> CreateRuleAsync(Guid productId, [FromBody] CreateRecurringRuleDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var rule = await _recurringService.CreateRuleAsync(productId, caller.UserId, dto, ct);
            return Created($"/api/v1/recurring-rules/{rule.Id}", Envelope(rule));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Updates a recurring rule.</summary>
    [HttpPut("api/v1/recurring-rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateRuleAsync(Guid ruleId, [FromBody] UpdateRecurringRuleDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var rule = await _recurringService.UpdateRuleAsync(ruleId, dto, caller.UserId, ct);
            return Ok(Envelope(rule));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    /// <summary>Deletes a recurring rule.</summary>
    [HttpDelete("api/v1/recurring-rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _recurringService.DeleteRuleAsync(ruleId, ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>
    /// Manually triggers processing of all due recurring rules.
    /// Useful for testing or admin intervention.
    /// </summary>
    [HttpPost("api/v1/recurring-rules/process")]
    public async Task<IActionResult> ProcessDueItemsAsync(CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var createdIds = await _recurringService.ProcessDueRecurringItemsAsync(ct);
            return Ok(Envelope(new { success = true, createdItemCount = createdIds.Count, createdItemIds = createdIds }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recurring rules");
            return StatusCode(500, ErrorEnvelope(ErrorCodes.InternalServerError, "Failed to process recurring rules."));
        }
    }
}
