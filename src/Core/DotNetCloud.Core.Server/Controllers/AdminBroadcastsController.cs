using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin endpoints for composing and managing broadcasts to all logged-in users
/// (e.g. a warning about an upcoming server reboot).
/// </summary>
[ApiController]
[Route("api/v1/core/admin/broadcasts")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public sealed class AdminBroadcastsController : ControllerBase
{
    private const string ModuleId = "dotnetcloud.core";

    private readonly IAdminBroadcastService _broadcastService;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AdminBroadcastsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminBroadcastsController"/> class.
    /// </summary>
    public AdminBroadcastsController(
        IAdminBroadcastService broadcastService,
        IAuditLogger auditLogger,
        ILogger<AdminBroadcastsController> logger)
    {
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists broadcasts for the admin history view, newest first.
    /// </summary>
    /// <param name="take">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The broadcast history.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var broadcasts = await _broadcastService.ListAsync(take, cancellationToken);
        return Ok(new { success = true, data = broadcasts });
    }

    /// <summary>
    /// Creates a broadcast and delivers it immediately, or schedules it for later.
    /// </summary>
    /// <param name="request">The broadcast content and delivery options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created broadcast.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAdminBroadcastRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "A request body is required." } });
        }

        if (!TryGetUserId(out var adminUserId) || adminUserId == Guid.Empty)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = "Title and message are required." } });
        }

        AdminBroadcastDto broadcast;
        try
        {
            broadcast = await _broadcastService.CreateAsync(request, adminUserId, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_REQUEST", message = ex.Message } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = ModuleId,
            Action = AuditAction.Create,
            EntityType = "AdminBroadcast",
            EntityId = broadcast.Id,
            Description = $"admin-broadcast-created:{broadcast.Severity}:{SanitizeForLog(broadcast.Status.ToString())}",
        });

        _logger.LogInformation(
            "Admin broadcast {BroadcastId} created by {AdminUserId} ({Severity} / {Status})",
            broadcast.Id, adminUserId, broadcast.Severity, broadcast.Status);

        return Ok(new { success = true, data = broadcast });
    }

    /// <summary>
    /// Delivers a scheduled broadcast immediately.
    /// </summary>
    /// <param name="id">The broadcast to deliver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The broadcast history entry.</returns>
    [HttpPost("{id:guid}/send-now")]
    public async Task<IActionResult> SendNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sent = await _broadcastService.SendNowAsync(id, cancellationToken);
        if (!sent)
        {
            return NotFound(new { success = false, error = new { code = "BROADCAST_NOT_SENT", message = "Broadcast was not found or has already been sent." } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = ModuleId,
            Action = AuditAction.Update,
            EntityType = "AdminBroadcast",
            EntityId = id,
            Description = "admin-broadcast-sent-now",
        });

        _logger.LogInformation("Admin broadcast {BroadcastId} sent immediately by admin", id);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Permanently deletes a broadcast. Dismissal records are removed as well.
    /// </summary>
    /// <param name="id">The broadcast to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content when the broadcast was deleted.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _broadcastService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { success = false, error = new { code = "BROADCAST_NOT_FOUND", message = $"Broadcast '{id}' not found." } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = ModuleId,
            Action = AuditAction.Delete,
            EntityType = "AdminBroadcast",
            EntityId = id,
            Description = "admin-broadcast-deleted",
        });

        _logger.LogInformation("Admin broadcast {BroadcastId} deleted by admin", id);
        return NoContent();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return LogSanitizer.Sanitize(value);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private CallerContext BuildCaller()
    {
        if (!TryGetUserId(out var userId) || userId == Guid.Empty)
        {
            return CallerContext.CreateSystemContext();
        }

        var roles = User.FindAll("role")
            .Concat(User.FindAll(System.Security.Claims.ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CallerContext(userId, roles, CallerType.User);
    }
}
