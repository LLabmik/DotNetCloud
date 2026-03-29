using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card attachments.
/// </summary>
[Route("api/v1/cards/{cardId:guid}/attachments")]
public class AttachmentsController : TracksControllerBase
{
    private readonly AttachmentService _attachmentService;
    private readonly ILogger<AttachmentsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentsController"/> class.
    /// </summary>
    public AttachmentsController(AttachmentService attachmentService, ILogger<AttachmentsController> logger)
    {
        _attachmentService = attachmentService;
        _logger = logger;
    }

    /// <summary>Lists attachments on a card.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAttachmentsAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var attachments = await _attachmentService.GetAttachmentsAsync(cardId, caller);
            return Ok(Envelope(attachments));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Adds an attachment to a card.</summary>
    [HttpPost]
    public async Task<IActionResult> AddAttachmentAsync(Guid cardId, [FromBody] AddAttachmentRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var attachment = await _attachmentService.AddAttachmentAsync(
                cardId, request.FileName, request.FileNodeId, request.Url, caller);
            return Created($"/api/v1/cards/{cardId}/attachments", Envelope(attachment));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Removes an attachment from a card.</summary>
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> RemoveAttachmentAsync(Guid cardId, Guid attachmentId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _attachmentService.RemoveAttachmentAsync(attachmentId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }
}

/// <summary>Request body for adding an attachment.</summary>
public sealed record AddAttachmentRequest
{
    /// <summary>Display name of the file.</summary>
    public required string FileName { get; init; }

    /// <summary>Optional Files module FileNode ID.</summary>
    public Guid? FileNodeId { get; init; }

    /// <summary>Optional external URL.</summary>
    public string? Url { get; init; }
}
