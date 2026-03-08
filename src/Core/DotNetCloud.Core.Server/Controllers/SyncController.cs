using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// REST API controller for file sync operations (desktop/mobile clients).
/// </summary>
[Route("api/v1/files/sync")]
public sealed class SyncController : FilesControllerBase
{
    private readonly ISyncService _syncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncController"/> class.
    /// </summary>
    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Gets all changes since a given timestamp.
    /// </summary>
    [HttpGet("changes")]
    public Task<IActionResult> GetChangesAsync(
        [FromQuery] DateTime since,
        [FromQuery] Guid? folderId,
        [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var changes = await _syncService.GetChangesSinceAsync(since, folderId, ToCaller(userId));
        return Ok(Envelope(changes));
    });

    /// <summary>
    /// Gets a full folder tree snapshot with content hashes.
    /// </summary>
    [HttpGet("tree")]
    public Task<IActionResult> GetTreeAsync(
        [FromQuery] Guid? folderId,
        [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var tree = await _syncService.GetFolderTreeAsync(folderId, ToCaller(userId));
        return Ok(Envelope(tree));
    });

    /// <summary>
    /// Reconciles client state against the server.
    /// </summary>
    [HttpPost("reconcile")]
    public Task<IActionResult> ReconcileAsync(
        [FromBody] SyncReconcileRequestDto request,
        [FromQuery] Guid userId) => ExecuteAsync(async () =>
    {
        var result = await _syncService.ReconcileAsync(request, ToCaller(userId));
        return Ok(Envelope(result));
    });
}
