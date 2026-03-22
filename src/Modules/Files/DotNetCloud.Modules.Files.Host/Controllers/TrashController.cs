using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for trash bin operations.
/// </summary>
[Route("api/v1/files/trash")]
public class TrashController : FilesControllerBase
{
    private readonly ITrashService _trashService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrashController"/> class.
    /// </summary>
    public TrashController(ITrashService trashService)
    {
        _trashService = trashService;
    }

    /// <summary>
    /// Lists all items in the trash bin for the authenticated user.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync() => ExecuteAsync(async () =>
    {
        var items = await _trashService.ListTrashAsync(GetAuthenticatedCaller());
        return Ok(Envelope(items));
    });

    /// <summary>
    /// Gets the total size of items in the trash.
    /// </summary>
    [HttpGet("size")]
    public Task<IActionResult> GetSizeAsync() => ExecuteAsync(async () =>
    {
        var size = await _trashService.GetTrashSizeAsync(GetAuthenticatedCaller());
        return Ok(Envelope(new { sizeBytes = size }));
    });

    /// <summary>
    /// Restores a trashed item to its original location.
    /// </summary>
    [HttpPost("{nodeId:guid}/restore")]
    public Task<IActionResult> RestoreAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        var node = await _trashService.RestoreAsync(nodeId, GetAuthenticatedCaller());
        return Ok(Envelope(node));
    });

    /// <summary>
    /// Permanently deletes a trashed item.
    /// </summary>
    [HttpDelete("{nodeId:guid}")]
    public Task<IActionResult> PurgeAsync(Guid nodeId) => ExecuteAsync(async () =>
    {
        await _trashService.PermanentDeleteAsync(nodeId, GetAuthenticatedCaller());
        return Ok(Envelope(new { deleted = true }));
    });

    /// <summary>
    /// Empties the entire trash bin for the authenticated user.
    /// </summary>
    [HttpDelete]
    public Task<IActionResult> EmptyAsync() => ExecuteAsync(async () =>
    {
        await _trashService.EmptyTrashAsync(GetAuthenticatedCaller());
        return Ok(Envelope(new { emptied = true }));
    });
}
