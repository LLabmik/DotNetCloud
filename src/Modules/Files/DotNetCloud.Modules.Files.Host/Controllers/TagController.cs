using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file/folder tag operations.
/// </summary>
[Route("api/v1/files")]
public class TagController : FilesControllerBase
{
    private readonly ITagService _tagService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagController"/> class.
    /// </summary>
    public TagController(ITagService tagService)
    {
        _tagService = tagService;
    }

    /// <summary>
    /// Adds a tag to a file or folder.
    /// </summary>
    [HttpPost("{nodeId:guid}/tags")]
    public Task<IActionResult> AddAsync(Guid nodeId, [FromBody] AddTagDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var tag = await _tagService.AddTagAsync(nodeId, dto.Name, dto.Color, ToCaller(userId));
        return Created($"/api/v1/files/{nodeId}/tags/{tag.Name}", Envelope(tag));
    });

    /// <summary>
    /// Removes a tag from a file or folder by name.
    /// </summary>
    [HttpDelete("{nodeId:guid}/tags/{tagName}")]
    public Task<IActionResult> RemoveAsync(Guid nodeId, string tagName, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _tagService.RemoveTagByNameAsync(nodeId, tagName, ToCaller(userId));
        return Ok(Envelope(new { deleted = true }));
    });

    /// <summary>
    /// Lists all of the user's tags (unique tag names).
    /// </summary>
    [HttpGet("tags")]
    public Task<IActionResult> ListAllTagsAsync([FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var tags = await _tagService.GetAllUserTagsAsync(ToCaller(userId));
        return Ok(Envelope(tags));
    });

    /// <summary>
    /// Lists all files with a specific tag.
    /// </summary>
    [HttpGet("tags/{tagName}")]
    public Task<IActionResult> ListByTagAsync(string tagName, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var nodes = await _tagService.GetNodesByTagAsync(tagName, ToCaller(userId));
        return Ok(Envelope(nodes));
    });
}
