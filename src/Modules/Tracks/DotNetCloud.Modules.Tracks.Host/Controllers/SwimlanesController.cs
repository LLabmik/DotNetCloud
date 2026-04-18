using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for board swimlane (column) management.
/// </summary>
[Route("api/v1/boards/{boardId:guid}/swimlanes")]
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

    /// <summary>Lists all swimlanes (columns) for a board.</summary>
    [HttpGet]
    public async Task<IActionResult> ListSwimlanesAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlanes = await _swimlaneService.GetSwimlanesAsync(boardId, caller);
            return Ok(Envelope(swimlanes));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a new swimlane on a board.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSwimlaneAsync(Guid boardId, [FromBody] CreateBoardSwimlaneDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _swimlaneService.CreateSwimlaneAsync(boardId, dto, caller);
            return Created($"/api/v1/boards/{boardId}/swimlanes", Envelope(swimlane));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a swimlane.</summary>
    [HttpPut("{swimlaneId:guid}")]
    public async Task<IActionResult> UpdateSwimlaneAsync(Guid boardId, Guid swimlaneId, [FromBody] UpdateBoardSwimlaneDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var swimlane = await _swimlaneService.UpdateSwimlaneAsync(swimlaneId, dto, caller);
            return Ok(Envelope(swimlane));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardSwimlaneNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a swimlane.</summary>
    [HttpDelete("{swimlaneId:guid}")]
    public async Task<IActionResult> DeleteSwimlaneAsync(Guid boardId, Guid swimlaneId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _swimlaneService.DeleteSwimlaneAsync(swimlaneId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message));
        }
    }

    /// <summary>Reorders swimlanes within a board.</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderSwimlanesAsync(Guid boardId, [FromBody] ReorderSwimlanesRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _swimlaneService.ReorderSwimlanesAsync(boardId, request.SwimlaneIds, caller);
            return Ok(Envelope(new { reordered = true }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}

/// <summary>Request body for reordering swimlanes.</summary>
public sealed record ReorderSwimlanesRequest
{
    /// <summary>Ordered list of swimlane IDs.</summary>
    public required IReadOnlyList<Guid> SwimlaneIds { get; init; }
}
