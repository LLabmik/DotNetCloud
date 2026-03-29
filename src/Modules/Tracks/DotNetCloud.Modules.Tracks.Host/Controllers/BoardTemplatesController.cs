using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for board template management.
/// </summary>
[Route("api/v1/tracks/board-templates")]
public class BoardTemplatesController : TracksControllerBase
{
    private readonly BoardTemplateService _boardTemplateService;
    private readonly ILogger<BoardTemplatesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardTemplatesController"/> class.
    /// </summary>
    public BoardTemplatesController(BoardTemplateService boardTemplateService, ILogger<BoardTemplatesController> logger)
    {
        _boardTemplateService = boardTemplateService;
        _logger = logger;
    }

    /// <summary>Lists all available board templates (built-in + user-created).</summary>
    [HttpGet]
    public async Task<IActionResult> ListTemplatesAsync()
    {
        var caller = GetAuthenticatedCaller();
        var templates = await _boardTemplateService.ListTemplatesAsync(caller);
        return Ok(Envelope(templates));
    }

    /// <summary>Gets a specific board template.</summary>
    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> GetTemplateAsync(Guid templateId)
    {
        var caller = GetAuthenticatedCaller();
        var template = await _boardTemplateService.GetTemplateAsync(templateId, caller);
        return template is null
            ? NotFound(ErrorEnvelope(ErrorCodes.BoardTemplateNotFound, "Board template not found."))
            : Ok(Envelope(template));
    }

    /// <summary>Creates a board from a template.</summary>
    [HttpPost("{templateId:guid}/use")]
    public async Task<IActionResult> CreateBoardFromTemplateAsync(Guid templateId, [FromBody] CreateBoardFromTemplateDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var board = await _boardTemplateService.CreateBoardFromTemplateAsync(templateId, dto, caller);
            return Created($"/api/v1/boards/{board.Id}", Envelope(board));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardTemplateNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardTemplateNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Saves an existing board as a template.</summary>
    [HttpPost("from-board/{boardId:guid}")]
    public async Task<IActionResult> SaveBoardAsTemplateAsync(Guid boardId, [FromBody] SaveBoardAsTemplateDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var template = await _boardTemplateService.SaveBoardAsTemplateAsync(boardId, dto, caller);
            return Created($"/api/v1/tracks/board-templates/{template.Id}", Envelope(template));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a user-created board template.</summary>
    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplateAsync(Guid templateId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _boardTemplateService.DeleteTemplateAsync(templateId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.BoardTemplateNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardTemplateNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
