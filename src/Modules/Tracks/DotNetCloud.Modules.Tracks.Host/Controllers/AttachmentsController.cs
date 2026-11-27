using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for work item attachments.
/// </summary>
[ApiController]
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

    /// <summary>Lists attachments on a work item.</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/attachments")]
    public async Task<IActionResult> GetAttachmentsAsync(Guid workItemId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var attachments = await _attachmentService.GetAttachmentsByWorkItemAsync(workItemId, ct);
        return Ok(Envelope(attachments));
    }

    /// <summary>Adds an attachment to a work item.</summary>
    [HttpPost("api/v1/workitems/{workItemId:guid}/attachments")]
    public async Task<IActionResult> AddAttachmentAsync(Guid workItemId, [FromBody] AddAttachmentRequest request, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var attachment = await _attachmentService.AddAttachmentAsync(
                workItemId,
                caller.UserId,
                request.FileName,
                request.FileSize,
                request.MimeType,
                request.FileNodeId,
                request.Url,
                ct);
            return Created($"/api/v1/workitems/{workItemId}/attachments", Envelope(attachment));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Removes an attachment from a work item.</summary>
    [HttpDelete("api/v1/workitems/{workItemId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> RemoveAttachmentAsync(Guid workItemId, Guid attachmentId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        await _attachmentService.RemoveAttachmentAsync(attachmentId, ct);
        return Ok(Envelope(new { deleted = true }));
    }
}

/// <summary>Request body for adding an attachment.</summary>
public sealed record AddAttachmentRequest
{
    /// <summary>Display name of the file.</summary>
    public required string FileName { get; init; }

    /// <summary>File size in bytes.</summary>
    public long? FileSize { get; init; }

    /// <summary>MIME type of the file.</summary>
    public string? MimeType { get; init; }

    /// <summary>Optional Files module FileNode ID.</summary>
    public Guid? FileNodeId { get; init; }

    /// <summary>Optional external URL.</summary>
    public string? Url { get; init; }
}
