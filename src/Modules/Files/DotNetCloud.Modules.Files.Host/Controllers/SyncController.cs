using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace DotNetCloud.Modules.Files.Host.Controllers;

/// <summary>
/// REST API controller for file sync operations (desktop/mobile clients).
/// </summary>
[Route("api/v1/files/sync")]
public class SyncController : FilesControllerBase
{
    private readonly ISyncService _syncService;
    private readonly ISyncChangeNotifier _syncNotifier;
    private readonly ILogger<SyncController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncController"/> class.
    /// </summary>
    public SyncController(ISyncService syncService, ISyncChangeNotifier syncNotifier, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _syncNotifier = syncNotifier;
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

    /// <summary>
    /// Server-Sent Events endpoint for real-time sync change notifications.
    /// Clients should keep this connection open to receive push notifications
    /// when the user's file tree changes, eliminating the need for frequent polling.
    /// </summary>
    /// <remarks>
    /// Each notification is a JSON object: <c>{ "type": "sync-changed", "latestSequence": 157 }</c>.
    /// The client should trigger a <c>sync/changes</c> poll upon receiving a notification.
    /// Falls back to a 30-second keepalive comment to prevent proxy timeouts.
    /// Maximum <c>25</c> concurrent SSE connections per user.
    /// </remarks>
    [HttpGet("stream")]
    [EnableRateLimiting("module-sync-stream")]
    public async Task StreamAsync(CancellationToken cancellationToken)
    {
        var caller = GetAuthenticatedCaller();
        var userId = caller.UserId;

        // Check connection limit before starting the stream.
        if (_syncNotifier.GetConnectionCount(userId) >= 25)
        {
            Response.StatusCode = 429;
            Response.ContentType = "application/json";
            await Response.WriteAsync(
                JsonSerializer.Serialize(ErrorEnvelope("SSE_LIMIT_REACHED", "Maximum SSE connections reached for this user.")),
                cancellationToken);
            return;
        }

        Response.StatusCode = 200;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // Nginx: disable proxy buffering

        await Response.Body.FlushAsync(cancellationToken);

        _logger.LogInformation("SSE stream opened for user {UserId}.", userId);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, HttpContext.RequestAborted);

        // Keepalive timer: send a comment every 30s to keep proxies from closing the connection.
        using var keepaliveTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        var keepaliveTask = Task.Run(async () =>
        {
            try
            {
                while (await keepaliveTimer.WaitForNextTickAsync(linkedCts.Token))
                {
                    await Response.WriteAsync(": keepalive\n\n", linkedCts.Token);
                    await Response.Body.FlushAsync(linkedCts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }, linkedCts.Token);

        try
        {
            await foreach (var notification in _syncNotifier.SubscribeAsync(userId, linkedCts.Token))
            {
                var data = JsonSerializer.Serialize(new
                {
                    type = "sync-changed",
                    latestSequence = notification.LatestSequence,
                    timestamp = notification.Timestamp,
                });

                await Response.WriteAsync($"event: sync-changed\ndata: {data}\n\n", linkedCts.Token);
                await Response.Body.FlushAsync(linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal shutdown
        }
        finally
        {
            await linkedCts.CancelAsync();
            _logger.LogInformation("SSE stream closed for user {UserId}.", userId);
        }
    }

    /// <summary>
    /// Acknowledges that the client has successfully processed changes up to a given sequence.
    /// Updates the server-side per-device cursor for crash recovery and admin visibility.
    /// </summary>
    [HttpPost("ack")]
    [EnableRateLimiting("module-sync-changes")]
    public Task<IActionResult> AcknowledgeCursorAsync(
        [FromBody] SyncCursorAckDto request) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        await _syncService.AcknowledgeCursorAsync(caller.UserId, request.DeviceId, request.AcknowledgedSequence, cancellationToken: HttpContext.RequestAborted);
        return Ok(Envelope(new { acknowledged = true }));
    });

    /// <summary>
    /// Gets the server-side cursor for a specific device. Used for cursor recovery
    /// after reinstallation or state database corruption.
    /// </summary>
    [HttpGet("device-cursor")]
    [EnableRateLimiting("module-sync-changes")]
    public Task<IActionResult> GetDeviceCursorAsync(
        [FromQuery] Guid deviceId) => ExecuteAsync(async () =>
    {
        var caller = GetAuthenticatedCaller();
        var cursor = await _syncService.GetDeviceCursorAsync(caller.UserId, deviceId, HttpContext.RequestAborted);
        return Ok(Envelope(cursor));
    });

    /// <summary>
    /// Returns sync status for all registered devices across all users.
    /// Admin-only endpoint for monitoring per-device sync lag.
    /// </summary>
    [HttpGet("admin/device-status")]
    [Authorize(Policy = "RequireAdmin")]
    public Task<IActionResult> GetAllDeviceSyncStatusAsync() => ExecuteAsync(async () =>
    {
        var statuses = await _syncService.GetAllDeviceSyncStatusAsync(HttpContext.RequestAborted);
        return Ok(Envelope(statuses));
    });

    /// <summary>
    /// Activates or deactivates a sync device. Inactive devices are rejected
    /// during device resolution and cannot sync.
    /// </summary>
    [HttpPut("admin/device/{deviceId:guid}/active")]
    [Authorize(Policy = "RequireAdmin")]
    public Task<IActionResult> SetDeviceActiveAsync(
        Guid deviceId,
        [FromBody] SetDeviceActiveDto request) => ExecuteAsync(async () =>
    {
        await _syncService.SetDeviceActiveAsync(deviceId, request.IsActive, HttpContext.RequestAborted);
        return Ok(Envelope(new { deviceId, isActive = request.IsActive }));
    });
}
