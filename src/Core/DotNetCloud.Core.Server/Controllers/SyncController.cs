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
        [FromQuery] Guid? folderId) => ExecuteAsync(async () =>
    {
        // Npgsql requires DateTime.Kind == Utc for timestamptz columns;
        // ASP.NET model binding parses query strings as Kind=Unspecified.
        var sinceUtc = DateTime.SpecifyKind(since, DateTimeKind.Utc);
        var changes = await _syncService.GetChangesSinceAsync(sinceUtc, folderId, GetAuthenticatedCaller());
        return Ok(changes);
    });

    /// <summary>
    /// Gets a full folder tree snapshot with content hashes.
    /// </summary>
    [HttpGet("tree")]
    public Task<IActionResult> GetTreeAsync(
        [FromQuery] Guid? folderId) => ExecuteAsync(async () =>
    {
        var tree = await _syncService.GetFolderTreeAsync(folderId, GetAuthenticatedCaller());
        return Ok(tree);
    });

    /// <summary>
    /// Reconciles client state against the server.
    /// </summary>
    [HttpPost("reconcile")]
    public Task<IActionResult> ReconcileAsync(
        [FromBody] SyncReconcileRequestDto request) => ExecuteAsync(async () =>
    {
        var result = await _syncService.ReconcileAsync(request, GetAuthenticatedCaller());
        return Ok(result);
    });
}
