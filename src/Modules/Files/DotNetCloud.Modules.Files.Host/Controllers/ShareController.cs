using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file and folder sharing operations.
/// </summary>
[Route("api/v1/files/{nodeId:guid}/shares")]
public class ShareController : FilesControllerBase
{
    private readonly IShareService _shareService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShareController"/> class.
    /// </summary>
    public ShareController(IShareService shareService)
    {
        _shareService = shareService;
    }

    /// <summary>
    /// Lists all shares for a file or folder.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ListAsync(Guid nodeId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var shares = await _shareService.GetSharesAsync(nodeId, ToCaller(userId));
        return Ok(Envelope(shares));
    });

    /// <summary>
    /// Creates a new share for a file or folder.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> CreateAsync(Guid nodeId, [FromBody] CreateShareDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var share = await _shareService.CreateShareAsync(nodeId, dto, ToCaller(userId));
        return Created($"/api/v1/files/{nodeId}/shares/{share.Id}", Envelope(share));
    });

    /// <summary>
    /// Updates an existing share.
    /// </summary>
    [HttpPut("{shareId:guid}")]
    public Task<IActionResult> UpdateAsync(Guid nodeId, Guid shareId, [FromBody] UpdateShareDto dto, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var share = await _shareService.UpdateShareAsync(shareId, dto, ToCaller(userId));
        return Ok(Envelope(share));
    });

    /// <summary>
    /// Revokes (deletes) a share.
    /// </summary>
    [HttpDelete("{shareId:guid}")]
    public Task<IActionResult> RevokeAsync(Guid nodeId, Guid shareId, [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        await _shareService.DeleteShareAsync(shareId, ToCaller(userId));
        return Ok(Envelope(new { deleted = true }));
    });
}
