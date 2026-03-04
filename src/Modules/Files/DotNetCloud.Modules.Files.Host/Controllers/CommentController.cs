using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file/folder comment operations.
/// </summary>
[Route("api/v1/files")]
public class CommentController : FilesControllerBase
{
    private readonly ICommentService _commentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentController"/> class.
    /// </summary>
    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    /// <summary>
    /// Adds a comment to a file or folder.
    /// </summary>
    [HttpPost("{nodeId:guid}/comments")]
    public Task<IActionResult> AddAsync(Guid nodeId, [FromBody] AddCommentDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var comment = await _commentService.AddCommentAsync(nodeId, dto.Content, dto.ParentCommentId, ToCaller(userId));
        return Created($"/api/v1/files/comments/{comment.Id}", Envelope(comment));
    });

    /// <summary>
    /// Lists comments on a file or folder.
    /// </summary>
    [HttpGet("{nodeId:guid}/comments")]
    public Task<IActionResult> ListAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var comments = await _commentService.GetCommentsAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(comments));
    });

    /// <summary>
    /// Edits a comment.
    /// </summary>
    [HttpPut("comments/{commentId:guid}")]
    public Task<IActionResult> EditAsync(Guid commentId, [FromBody] EditCommentDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var comment = await _commentService.EditCommentAsync(commentId, dto.Content, ToCaller(userId));
        return Ok(Envelope(comment));
    });

    /// <summary>
    /// Deletes a comment.
    /// </summary>
    [HttpDelete("comments/{commentId:guid}")]
    public Task<IActionResult> DeleteAsync(Guid commentId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _commentService.DeleteCommentAsync(commentId, ToCaller(userId));
        return Ok(Envelope(new { deleted = true }));
    });
}
