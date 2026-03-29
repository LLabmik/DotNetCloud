using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for card comments.
/// </summary>
[Route("api/v1/cards/{cardId:guid}/comments")]
public class CommentsController : TracksControllerBase
{
    private readonly CommentService _commentService;
    private readonly ILogger<CommentsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentsController"/> class.
    /// </summary>
    public CommentsController(CommentService commentService, ILogger<CommentsController> logger)
    {
        _commentService = commentService;
        _logger = logger;
    }

    /// <summary>Lists comments on a card.</summary>
    [HttpGet]
    public async Task<IActionResult> ListCommentsAsync(Guid cardId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var comments = await _commentService.GetCommentsAsync(cardId, caller);
            return Ok(Envelope(comments));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message));
        }
    }

    /// <summary>Creates a comment on a card.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateCommentAsync(Guid cardId, [FromBody] CreateCommentRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var comment = await _commentService.CreateCommentAsync(cardId, request.Content, caller);
            return Created($"/api/v1/cards/{cardId}/comments", Envelope(comment));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CardNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CardNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Updates a comment.</summary>
    [HttpPut("{commentId:guid}")]
    public async Task<IActionResult> UpdateCommentAsync(Guid cardId, Guid commentId, [FromBody] UpdateCommentRequest request)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var comment = await _commentService.UpdateCommentAsync(commentId, request.Content, caller);
            return Ok(Envelope(comment));
        }
        catch (ValidationException ex)
        {
            return ex.Errors.ContainsKey(ErrorCodes.CommentNotFound)
                ? NotFound(ErrorEnvelope(ErrorCodes.CommentNotFound, ex.Message))
                : BadRequest(ErrorEnvelope(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Deletes a comment.</summary>
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteCommentAsync(Guid cardId, Guid commentId)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _commentService.DeleteCommentAsync(commentId, caller);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (ValidationException ex)
        {
            return NotFound(ErrorEnvelope(ErrorCodes.CommentNotFound, ex.Message));
        }
    }
}

/// <summary>Request body for creating a comment.</summary>
public sealed record CreateCommentRequest
{
    /// <summary>Markdown content of the comment.</summary>
    public required string Content { get; init; }
}

/// <summary>Request body for updating a comment.</summary>
public sealed record UpdateCommentRequest
{
    /// <summary>Updated markdown content.</summary>
    public required string Content { get; init; }
}
