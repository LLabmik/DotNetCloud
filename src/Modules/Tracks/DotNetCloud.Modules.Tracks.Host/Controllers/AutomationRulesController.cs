using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for automation rules.
/// </summary>
[ApiController]
public class AutomationRulesController : TracksControllerBase
{
    private readonly AutomationRuleService _ruleService;
    private readonly ILogger<AutomationRulesController> _logger;

    public AutomationRulesController(AutomationRuleService ruleService, ILogger<AutomationRulesController> logger)
    {
        _ruleService = ruleService;
        _logger = logger;
    }

    /// <summary>Lists all automation rules for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/automation-rules")]
    public async Task<IActionResult> ListAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var rules = await _ruleService.ListAsync(productId, ct);
        return Ok(Envelope(rules));
    }

    /// <summary>Gets a single automation rule.</summary>
    [HttpGet("api/v1/automation-rules/{ruleId:guid}")]
    public async Task<IActionResult> GetAsync(Guid ruleId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var rule = await _ruleService.GetAsync(ruleId, ct);
        return rule is null ? NotFound() : Ok(Envelope(rule));
    }

    /// <summary>Creates a new automation rule.</summary>
    [HttpPost("api/v1/products/{productId:guid}/automation-rules")]
    public async Task<IActionResult> CreateAsync(Guid productId, [FromBody] CreateAutomationRuleDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var rule = await _ruleService.CreateAsync(productId, dto, caller.UserId, ct);
        return CreatedAtAction(nameof(GetAsync), new { ruleId = rule.Id }, Envelope(rule));
    }

    /// <summary>Updates an automation rule.</summary>
    [HttpPut("api/v1/automation-rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid ruleId, [FromBody] UpdateAutomationRuleDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var rule = await _ruleService.UpdateAsync(ruleId, dto, ct);
        return rule is null ? NotFound() : Ok(Envelope(rule));
    }

    /// <summary>Deletes an automation rule.</summary>
    [HttpDelete("api/v1/automation-rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid ruleId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var deleted = await _ruleService.DeleteAsync(ruleId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
