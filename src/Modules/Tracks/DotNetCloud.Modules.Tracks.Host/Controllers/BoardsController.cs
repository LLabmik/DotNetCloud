using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for board management and membership.
/// </summary>
[Route("api/v1/boards")]
public class BoardsController : TracksControllerBase
{
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly LabelService _labelService;
    private readonly TeamService _teamService;
    private readonly ILogger<BoardsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardsController"/> class.
    /// </summary>
    public BoardsController(
        BoardService boardService,
        ActivityService activityService,
        LabelService labelService,
        TeamService teamService,
        ILogger<BoardsController> logger)
    {
        _boardService = boardService;
        _activityService = activityService;
        _labelService = labelService;
        _teamService = teamService;
        _logger = logger;
    }

    // ─── Board CRUD ───────────────────────────────────────────────────────

    /// <summary>Lists all boards for the authenticated user, optionally filtered by mode.</summary>
    [HttpGet]
    public async Task<IActionResult> ListBoardsAsync(
        [FromQuery] bool includeArchived = false,
        [FromQuery] BoardMode? mode = null)
    {
        var caller = GetAuthenticatedCaller();
        var boards = await _boardService.ListBoardsAsync(caller, includeArchived, mode);
        return Ok(Envelope(boards));
    }

    /// <summary>Gets a board by ID.</summary>
    [HttpGet("{boardId:guid}")]
    public async Task<IActionResult> GetBoardAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.GetBoardAsync(boardId, caller);
        return board is null
            ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Board not found."))
            : Ok(Envelope(board));
    }

    /// <summary>Creates a new board.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateBoardAsync([FromBody] CreateBoardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.CreateBoardAsync(dto, caller);
        return Created($"/api/v1/boards/{board.Id}", Envelope(board));
    }

    /// <summary>Updates a board.</summary>
    [HttpPut("{boardId:guid}")]
    public async Task<IActionResult> UpdateBoardAsync(Guid boardId, [FromBody] UpdateBoardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var board = await _boardService.UpdateBoardAsync(boardId, dto, caller);
            return Ok(Envelope(board));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a board.</summary>
    [HttpDelete("{boardId:guid}")]
    public async Task<IActionResult> DeleteBoardAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _boardService.DeleteBoardAsync(boardId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
    }

    /// <summary>Transfers a board to or from a team.</summary>
    [HttpPost("{boardId:guid}/transfer")]
    public async Task<IActionResult> TransferBoardAsync(
        Guid boardId,
        [FromBody] TransferBoardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _teamService.TransferBoardAsync(boardId, dto.TeamId, caller);
            return Ok(Envelope(new { transferred = true, teamId = dto.TeamId }));
        }
        catch (ValidationException ex)
        {
            if (IsBoardNotFound(ex))
                return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));

            return BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Board Activity ───────────────────────────────────────────────────

    /// <summary>Gets the activity feed for a board.</summary>
    [HttpGet("{boardId:guid}/activity")]
    public async Task<IActionResult> GetBoardActivityAsync(
        Guid boardId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.GetBoardAsync(boardId, caller);
        if (board is null)
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Board not found."));

        var activity = await _activityService.GetBoardActivityAsync(boardId, skip, take);
        return Ok(Envelope(activity));
    }

    // ─── Board Members ────────────────────────────────────────────────────

    /// <summary>Lists members of a board.</summary>
    [HttpGet("{boardId:guid}/members")]
    public async Task<IActionResult> ListMembersAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.GetBoardAsync(boardId, caller);
        if (board is null)
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Board not found."));

        return Ok(Envelope(board.Members));
    }

    /// <summary>Adds a member to a board.</summary>
    [HttpPost("{boardId:guid}/members")]
    public async Task<IActionResult> AddMemberAsync(
        Guid boardId,
        [FromBody] AddBoardMemberRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var member = await _boardService.AddMemberAsync(boardId, request.UserId, request.Role, caller);
            return Created($"/api/v1/boards/{boardId}/members", Envelope(member));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes a member from a board.</summary>
    [HttpDelete("{boardId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(Guid boardId, Guid userId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _boardService.RemoveMemberAsync(boardId, userId, caller);
            return Ok(Envelope(new { removed = true }));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a member's role on a board.</summary>
    [HttpPut("{boardId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRoleAsync(
        Guid boardId,
        Guid userId,
        [FromBody] UpdateMemberRoleRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _boardService.UpdateMemberRoleAsync(boardId, userId, request.Role, caller);
            return Ok(Envelope(new { updated = true }));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    // ─── Board Labels ─────────────────────────────────────────────────────

    /// <summary>Lists labels for a board.</summary>
    [HttpGet("{boardId:guid}/labels")]
    public async Task<IActionResult> ListLabelsAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var labels = await _labelService.GetLabelsAsync(boardId, caller);
            return Ok(Envelope(labels));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a label on a board.</summary>
    [HttpPost("{boardId:guid}/labels")]
    public async Task<IActionResult> CreateLabelAsync(Guid boardId, [FromBody] CreateLabelDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var label = await _labelService.CreateLabelAsync(boardId, dto, caller);
            return Created($"/api/v1/boards/{boardId}/labels", Envelope(label));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a label.</summary>
    [HttpPut("{boardId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> UpdateLabelAsync(Guid boardId, Guid labelId, [FromBody] UpdateLabelDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var label = await _labelService.UpdateLabelAsync(labelId, dto, caller);
            return Ok(Envelope(label));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.LabelNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.LabelNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a label from a board.</summary>
    [HttpDelete("{boardId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> DeleteLabelAsync(Guid boardId, Guid labelId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _labelService.DeleteLabelAsync(labelId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.LabelNotFound, ex.Message));
        }
    }

    // ─── Board Export/Import ──────────────────────────────────────────────

    /// <summary>Exports a board as JSON.</summary>
    [HttpGet("{boardId:guid}/export")]
    public async Task<IActionResult> ExportBoardAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.GetBoardAsync(boardId, caller);
        if (board is null)
            return NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, "Board not found."));

        return Ok(Envelope(board));
    }

    /// <summary>Imports a board from JSON.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportBoardAsync([FromBody] CreateBoardDto dto)
    {
        var caller = GetAuthenticatedCaller();
        var board = await _boardService.CreateBoardAsync(dto, caller);
        return Created($"/api/v1/boards/{board.Id}", Envelope(board));
    }
}

// ─── Request DTOs (Controller-level, not shared) ──────────────────────────

/// <summary>Request body for adding a board member.</summary>
public sealed record AddBoardMemberRequest
{
    /// <summary>The user ID to add.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The role to assign.</summary>
    public required BoardMemberRole Role { get; init; }
}

/// <summary>Request body for updating a member's role.</summary>
public sealed record UpdateMemberRoleRequest
{
    /// <summary>The new role.</summary>
    public required BoardMemberRole Role { get; init; }
}
