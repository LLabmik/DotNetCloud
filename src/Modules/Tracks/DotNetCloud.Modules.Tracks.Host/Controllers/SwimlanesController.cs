using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for swimlane (column) management on products and work items.
/// </summary>
[ApiController]
public class SwimlanesController : TracksControllerBase
{
    private readonly SwimlaneService _swimlaneService;
    private readonly ILogger<SwimlanesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwimlanesController"/> class.
    /// </summary>
    public SwimlanesController(SwimlaneService swimlaneService, ILogger<SwimlanesController> logger)
    {
        _swimlaneService = swimlaneService;
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
}
