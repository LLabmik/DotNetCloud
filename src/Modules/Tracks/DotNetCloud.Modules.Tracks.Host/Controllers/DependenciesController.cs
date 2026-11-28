using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for work item dependency relationships.
/// </summary>
[ApiController]
public class DependenciesController : TracksControllerBase
{
    private readonly DependencyService _dependencyService;
    private readonly ILogger<DependenciesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependenciesController"/> class.
    /// </summary>
    public DependenciesController(DependencyService dependencyService, ILogger<DependenciesController> logger)
    {
        _dependencyService = dependencyService;
        _logger = logger;
    }

    /// <summary>Lists all dependencies for a work item (items this work item depends on).</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/dependencies")]
    public async Task<IActionResult> GetDependenciesAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var dependencies = await _dependencyService.GetDependenciesByWorkItemAsync(workItemId, ct);
        return Ok(Envelope(dependencies));
    }

    /// <summary>Lists all dependents for a work item (items that depend on this work item).</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/dependents")]
    public async Task<IActionResult> GetDependentsAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var dependents = await _dependencyService.GetDependentsByWorkItemAsync(workItemId, ct);
        return Ok(Envelope(dependents));
    }

    /// <summary>Adds a dependency on another work item.</summary>
    [HttpPost("api/v1/workitems/{workItemId:guid}/dependencies")]
    public async Task<IActionResult> AddDependencyAsync(Guid workItemId, [FromBody] AddWorkItemDependencyDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var dependency = await _dependencyService.AddDependencyAsync(workItemId, dto, ct);
            return Created($"/api/v1/workitems/{workItemId}/dependencies", Envelope(dependency));
        }
        catch (System.InvalidOperationException ex)
        {
            return ex.Message.Contains("cannot depend on itself", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("circular chain", StringComparison.OrdinalIgnoreCase)
                ? Conflict(ErrorEnvelope(ErrorCodes.DependencyCycleDetected, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Removes a dependency by its ID.</summary>
    [HttpDelete("api/v1/workitems/{workItemId:guid}/dependencies/{dependencyId:guid}")]
    public async Task<IActionResult> RemoveDependencyAsync(Guid workItemId, Guid dependencyId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        await _dependencyService.RemoveDependencyAsync(dependencyId, ct);
        return Ok(Envelope(new { removed = true }));
    }
}
