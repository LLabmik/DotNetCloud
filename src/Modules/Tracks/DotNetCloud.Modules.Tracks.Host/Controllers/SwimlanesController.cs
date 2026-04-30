using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for swimlane (column) management on products and work items.
/// </summary>
[ApiController]
public class SwimlanesController : TracksControllerBase
{
    private readonly SwimlaneService _swimlaneService;
    private readonly SwimlaneTransitionService _transitionService;
    private readonly ILogger<SwimlanesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwimlanesController"/> class.
    /// </summary>
    public SwimlanesController(SwimlaneService swimlaneService, SwimlaneTransitionService transitionService, ILogger<SwimlanesController> logger)
    {
        _swimlaneService = swimlaneService;
        _transitionService = transitionService;
        _logger = logger;
    }

    /// <summary>Lists all swimlanes for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/swimlanes")]
    public async Task<IActionResult> GetProductSwimlanesAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(SwimlaneContainerType.Product, productId, ct);
            return Ok(Envelope(swimlanes));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Creates a new swimlane on a product.</summary>
    [HttpPost("api/v1/products/{productId:guid}/swimlanes")]
    public async Task<IActionResult> CreateProductSwimlaneAsync(Guid productId, [FromBody] CreateSwimlaneDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(SwimlaneContainerType.Product, productId, dto, ct);
            return Created($"/api/v1/products/{productId}/swimlanes", Envelope(swimlane));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Lists all swimlanes for a work item (sub-board).</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/swimlanes")]
    public async Task<IActionResult> GetWorkItemSwimlanesAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(SwimlaneContainerType.WorkItem, workItemId, ct);
            return Ok(Envelope(swimlanes));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Creates a new swimlane on a work item sub-board.</summary>
    [HttpPost("api/v1/workitems/{workItemId:guid}/swimlanes")]
    public async Task<IActionResult> CreateWorkItemSwimlaneAsync(Guid workItemId, [FromBody] CreateSwimlaneDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(SwimlaneContainerType.WorkItem, workItemId, dto, ct);
            return Created($"/api/v1/workitems/{workItemId}/swimlanes", Envelope(swimlane));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Updates a swimlane.</summary>
    [HttpPut("api/v1/swimlanes/{swimlaneId:guid}")]
    public async Task<IActionResult> UpdateSwimlaneAsync(Guid swimlaneId, [FromBody] UpdateSwimlaneDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _swimlaneService.UpdateSwimlaneAsync(swimlaneId, dto, ct);
            return Ok(Envelope(swimlane));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Deletes (archives) a swimlane.</summary>
    [HttpDelete("api/v1/swimlanes/{swimlaneId:guid}")]
    public async Task<IActionResult> DeleteSwimlaneAsync(Guid swimlaneId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _swimlaneService.DeleteSwimlaneAsync(swimlaneId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
    }

    /// <summary>Reorders swimlanes within a container.</summary>
    [HttpPut("api/v1/swimlanes/reorder")]
    public async Task<IActionResult> ReorderSwimlanesAsync([FromBody] ReorderSwimlanesDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var reordered = await _swimlaneService.ReorderSwimlanesAsync(dto.OrderedIds, ct);
            return Ok(Envelope(reordered));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    // ─── Transition Rules ─────────────────────────────────────────

    /// <summary>Gets the swimlane transition matrix for a product.</summary>
    [HttpGet("api/v1/products/{productId:guid}/swimlane-transitions")]
    public async Task<IActionResult> GetTransitionMatrixAsync(Guid productId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var rules = await _transitionService.GetTransitionMatrixAsync(productId, ct);
            return Ok(Envelope(rules));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
    }

    /// <summary>Sets (replaces) the swimlane transition matrix for a product.</summary>
    [HttpPut("api/v1/products/{productId:guid}/swimlane-transitions")]
    public async Task<IActionResult> SetTransitionMatrixAsync(Guid productId, [FromBody] List<SetTransitionRuleDto> rules, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var result = await _transitionService.SetTransitionMatrixAsync(productId, rules, ct);
            return Ok(Envelope(result));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.NotFound, ex.Message));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Gets the allowed target swimlanes for a given source swimlane.</summary>
    [HttpGet("api/v1/swimlanes/{swimlaneId:guid}/allowed-targets")]
    public async Task<IActionResult> GetAllowedTargetsAsync(Guid swimlaneId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            // We need the product ID and swimlane ID; get product ID from the swimlane
            var swimlane = await _swimlaneService.GetSwimlaneByIdAsync(swimlaneId, ct);
            if (swimlane is null)
                return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, "Swimlane not found."));

            // Determine product ID from container
            var productId = swimlane.ContainerType == SwimlaneContainerType.Product
                ? swimlane.ContainerId
                : Guid.Empty; // For work-item swimlanes, we need the root product

            if (productId == Guid.Empty)
                return Ok(Envelope(Array.Empty<Guid>())); // No restrictions on sub-boards

            var allowed = await _transitionService.GetAllowedTargetsAsync(productId, swimlaneId, ct);
            return Ok(Envelope(allowed));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
    }
}
