using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for bulk card operations.
/// </summary>
[Route("api/v1/boards/{boardId:guid}/bulk")]
public class BulkOperationsController : TracksControllerBase
{
    private readonly BulkOperationService _bulkService;
    private readonly ILogger<BulkOperationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkOperationsController"/> class.
    /// </summary>
    public BulkOperationsController(BulkOperationService bulkService, ILogger<BulkOperationsController> logger)
    {
        _bulkService = bulkService;
        _logger = logger;
    }

    /// <summary>Moves multiple cards to a target list.</summary>
    [HttpPost("cards/move")]
    public async Task<IActionResult> BulkMoveAsync([FromBody] BulkMoveCardsDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var result = await _bulkService.BulkMoveCardsAsync(dto, caller);
            return Ok(Envelope(result));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardSwimlaneNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardSwimlaneNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Assigns multiple cards to a user.</summary>
    [HttpPost("cards/assign")]
    public async Task<IActionResult> BulkAssignAsync([FromBody] BulkAssignCardsDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var result = await _bulkService.BulkAssignCardsAsync(dto, caller);
            return Ok(Envelope(result));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Applies a label to multiple cards.</summary>
    [HttpPost("cards/label")]
    public async Task<IActionResult> BulkLabelAsync([FromBody] BulkLabelCardsDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var result = await _bulkService.BulkLabelCardsAsync(dto, caller);
            return Ok(Envelope(result));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.LabelNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.LabelNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Archives multiple cards.</summary>
    [HttpPost("cards/archive")]
    public async Task<IActionResult> BulkArchiveAsync([FromBody] BulkCardOperationDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var result = await _bulkService.BulkArchiveCardsAsync(dto, caller);
            return Ok(Envelope(result));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
