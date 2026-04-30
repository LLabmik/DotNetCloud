using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Tracks.Host.Controllers;

/// <summary>
/// REST API controller for work item comments.
/// </summary>
[ApiController]
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

    /// <summary>Lists comments on a work item, with optional pagination.</summary>
    [HttpGet("api/v1/workitems/{workItemId:guid}/comments")]
    public async Task<IActionResult> GetCommentsAsync(Guid workItemId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var comments = await _commentService.GetCommentsByWorkItemAsync(workItemId, skip, take, ct);
        return Ok(Envelope(comments, new { skip, take }));
    }

    /// <summary>Creates a comment on a work item.</summary>
    [HttpPost("api/v1/workitems/{workItemId:guid}/comments")]
    public async Task<IActionResult> CreateCommentAsync(Guid workItemId, [FromBody] AddWorkItemCommentDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var comment = await _commentService.CreateCommentAsync(workItemId, caller.UserId, dto, ct);
            return Created($"/api/v1/workitems/{workItemId}/comments", Envelope(comment));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Updates a comment. Only the author may edit their own comment.</summary>
    [HttpPut("api/v1/workitems/{workItemId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateCommentAsync(Guid workItemId, Guid commentId, [FromBody] UpdateWorkItemCommentDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var comment = await _commentService.UpdateCommentAsync(commentId, caller.UserId, dto, ct);
            return Ok(Envelope(comment));
        }
        catch (System.InvalidOperationException ex)
        {
            return ex.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
                ? StatusCode(403, ErrorEnvelope(ErrorCodes.Forbidden, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Deletes a comment (soft delete). Only the author may delete their own comment.</summary>
    [HttpDelete("api/v1/workitems/{workItemId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _commentService.DeleteCommentAsync(commentId, caller.UserId, ct);
            return Ok(Envelope(new { deleted = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return ex.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
                ? StatusCode(403, ErrorEnvelope(ErrorCodes.Forbidden, ex.Message))
                : BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    // ── Reactions ──────────────────────────────────────────────────────────

    /// <summary>Gets all reactions for a comment, grouped by emoji with counts.</summary>
    [HttpGet("api/v1/comments/{commentId:guid}/reactions")]
    public async Task<IActionResult> GetReactionsAsync(Guid commentId, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        var reactions = await _commentService.GetReactionsAsync(commentId, caller.UserId, ct);
        return Ok(Envelope(reactions));
    }

    /// <summary>Adds an emoji reaction to a comment.</summary>
    [HttpPost("api/v1/comments/{commentId:guid}/reactions")]
    public async Task<IActionResult> AddReactionAsync(Guid commentId, [FromBody] AddReactionDto dto, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            var reaction = await _commentService.AddReactionAsync(commentId, caller.UserId, dto.Emoji.Trim(), ct);
            return Created($"/api/v1/comments/{commentId}/reactions", Envelope(reaction));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }

    /// <summary>Removes a user's emoji reaction from a comment.</summary>
    [HttpDelete("api/v1/comments/{commentId:guid}/reactions/{emoji}")]
    public async Task<IActionResult> RemoveReactionAsync(Guid commentId, string emoji, CancellationToken ct)
    {
        var caller = GetAuthenticatedCaller();
        try
        {
            await _commentService.RemoveReactionAsync(commentId, caller.UserId, emoji, ct);
            return Ok(Envelope(new { removed = true }));
        }
        catch (System.InvalidOperationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.InvalidOperation, ex.Message));
        }
    }
}
