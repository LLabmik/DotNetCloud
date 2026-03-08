using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file sync operations (desktop/mobile clients).
/// </summary>
[Route("api/v1/files/sync")]
public class SyncController : FilesControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncController"/> class.
    /// </summary>
    public SyncController(ISyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
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
        var caller = GetAuthenticatedCaller();
        var sw = Stopwatch.StartNew();
        var result = await _syncService.ReconcileAsync(request, caller);
        sw.Stop();
        _logger.LogInformation("sync.reconcile.completed {UserId} {ChangeCount} {DurationMs}",
            caller.UserId, result.Actions.Count, sw.ElapsedMilliseconds);
        return Ok(result);
    });
}
