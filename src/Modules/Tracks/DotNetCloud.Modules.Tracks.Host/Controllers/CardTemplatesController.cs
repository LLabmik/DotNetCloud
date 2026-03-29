using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card template management.
/// </summary>
[Route("api/v1/boards/{boardId:guid}/card-templates")]
public class CardTemplatesController : TracksControllerBase
{
    private readonly CardTemplateService _cardTemplateService;
    private readonly ILogger<CardTemplatesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardTemplatesController"/> class.
    /// </summary>
    public CardTemplatesController(CardTemplateService cardTemplateService, ILogger<CardTemplatesController> logger)
    {
        _cardTemplateService = cardTemplateService;
        _logger = logger;
    }

    /// <summary>Lists card templates for a board.</summary>
    [HttpGet]
    public async Task<IActionResult> ListTemplatesAsync(Guid boardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var templates = await _cardTemplateService.ListTemplatesAsync(boardId, caller);
            return Ok(Envelope(templates));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Gets a specific card template.</summary>
    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> GetTemplateAsync(Guid boardId, Guid templateId)
    {
        var caller = GetAuthenticatedCaller();
        var template = await _cardTemplateService.GetTemplateAsync(templateId, caller);
        return template is null
            ? NotFound(ErrorEnvelope(ErrorCodes.CardTemplateNotFound, "Card template not found."))
            : Ok(Envelope(template));
    }

    /// <summary>Saves a card as a template.</summary>
    [HttpPost("from-card/{cardId:guid}")]
    public async Task<IActionResult> SaveCardAsTemplateAsync(Guid boardId, Guid cardId, [FromBody] SaveCardAsTemplateDto dto)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var template = await _cardTemplateService.SaveCardAsTemplateAsync(cardId, dto, caller);
            return Created($"/api/v1/boards/{boardId}/card-templates/{template.Id}", Envelope(template));
        }
        catch (ValidationException ex)
        {
            return IsBoardNotFound(ex)
                ? NotFound(ErrorEnvelope(ErrorCodes.BoardNotFound, ex.Message))
                : ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                    ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                    : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a card template.</summary>
    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplateAsync(Guid boardId, Guid templateId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _cardTemplateService.DeleteTemplateAsync(templateId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardTemplateNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardTemplateNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }
}
