using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    /// Gets changes since a given cursor or timestamp.
    /// </summary>
    /// <remarks>
    /// Cursor-aware clients: pass <c>cursor</c> from the previous response's <c>nextCursor</c>.
    /// Legacy clients: pass <c>since</c> (datetime) — cursor/pagination is bypassed.
    /// Default page size is 500; maximum is 5000.
    /// </remarks>
    [HttpGet("changes")]
    [EnableRateLimiting("module-sync-changes")]
    public Task<IActionResult> GetChangesAsync(
        [FromQuery] string? cursor,
        [FromQuery] DateTime? since,
        [FromQuery] int limit = 500,
        [FromQuery] Guid? folderId = null) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();

        // Legacy path: client supplies a timestamp, no cursor support
        if (since.HasValue && cursor is null)
        {
            var sinceUtc = DateTime.SpecifyKind(since.Value, DateTimeKind.Utc);
            var changes = await _syncService.GetChangesSinceAsync(sinceUtc, folderId, caller);
            return Ok(changes);
        }

        // Cursor path (new default)
        var paged = await _syncService.GetChangesSinceCursorAsync(cursor, folderId, limit, caller);
        return Ok(paged);
    });

    /// <summary>
    /// Gets a full folder tree snapshot with content hashes.
    /// </summary>
    [HttpGet("tree")]
    [EnableRateLimiting("module-sync-tree")]
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
    [EnableRateLimiting("module-sync-reconcile")]
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
