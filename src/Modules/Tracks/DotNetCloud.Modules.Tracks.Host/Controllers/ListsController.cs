using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for board list (column) management.
/// </summary>
[Route("api/v1/boards/{boardId:guid}/lists")]
public class ListsController : TracksControllerBase
{
    private readonly ListService _listService;
    private readonly ILogger<ListsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListsController"/> class.
    /// </summary>
    public ListsController(ListService listService, ILogger<ListsController> logger)
    {
        _listService = listService;
        _logger = logger;
    }

    /// <summary>Lists all lists (columns) for a board.</summary>
    [HttpGet]
    public async Task<IActionResult> ListListsAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var lists = await _listService.GetListsAsync(boardId, caller);
            return Ok(Envelope(lists));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a new list on a board.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateListAsync(Guid boardId, [FromBody] CreateBoardListDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var list = await _listService.CreateListAsync(boardId, dto, caller);
            return Created($"/api/v1/boards/{boardId}/lists", Envelope(list));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a list.</summary>
    [HttpPut("{listId:guid}")]
    public async Task<IActionResult> UpdateListAsync(Guid boardId, Guid listId, [FromBody] UpdateBoardListDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var list = await _listService.UpdateListAsync(listId, dto, caller);
            return Ok(Envelope(list));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardListNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardListNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a list.</summary>
    [HttpDelete("{listId:guid}")]
    public async Task<IActionResult> DeleteListAsync(Guid boardId, Guid listId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _listService.DeleteListAsync(listId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardListNotFound, ex.Message));
        }
    }

    /// <summary>Reorders lists within a board.</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderListsAsync(Guid boardId, [FromBody] ReorderListsRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _listService.ReorderListsAsync(boardId, request.ListIds, caller);
            return Ok(Envelope(new { reordered = true }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}

/// <summary>Request body for reordering lists.</summary>
public sealed record ReorderListsRequest
{
    /// <summary>Ordered list of list IDs.</summary>
    public required IReadOnlyList<Guid> ListIds { get; init; }
}
