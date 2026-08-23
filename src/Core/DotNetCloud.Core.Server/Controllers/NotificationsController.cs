using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// REST API for user notifications.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationPreferenceStore _preferenceStore;

    public NotificationsController(INotificationService notificationService, INotificationPreferenceStore preferenceStore)
    {
        _notificationService = notificationService;
        _preferenceStore = preferenceStore;
    }

    /// <summary>
    /// Gets unread notifications for the current user.
    /// </summary>
    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadAsync([FromQuery] int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notificationService.GetUnreadAsync(userId, maxResults, cancellationToken);
        return Ok(new { data = notifications });
    }

    /// <summary>
    /// Gets unread notification count for the current user.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { data = count });
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [HttpPost("{notificationId:guid}/mark-read")]
    public async Task<IActionResult> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _notificationService.MarkReadAsync(notificationId, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks all notifications as read for the current user.
    /// </summary>
    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _notificationService.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// DTO for updating notification preferences.
    /// </summary>
    public sealed record UpdateNotificationPreferencesDto
    {
        /// <summary>Whether push notifications are globally enabled.</summary>
        public bool PushEnabled { get; init; } = true;

        /// <summary>Whether do-not-disturb mode is enabled (default: disabled).</summary>
        public bool DoNotDisturb { get; init; } = false;

        /// <summary>Channel IDs muted for push notifications.</summary>
        public List<Guid> MutedChannelIds { get; init; } = new();
    }

    /// <summary>
    /// Gets notification preferences for the current user.
    /// </summary>
    [HttpGet("preferences")]
    public IActionResult GetPreferences()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var prefs = _preferenceStore.Get(userId);
        return Ok(new
        {
            pushEnabled = prefs.PushEnabled,
            doNotDisturb = prefs.DoNotDisturb,
            mutedChannelIds = prefs.MutedChannelIds
        });
    }

    /// <summary>
    /// Updates notification preferences for the current user.
    /// </summary>
    [HttpPut("preferences")]
    public IActionResult UpdatePreferences([FromBody] UpdateNotificationPreferencesDto dto)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var preferences = new UserNotificationPreferences
        {
            PushEnabled = dto.PushEnabled,
            DoNotDisturb = dto.DoNotDisturb,
            MutedChannelIds = dto.MutedChannelIds.Distinct().ToHashSet()
        };

        _preferenceStore.Update(userId, preferences);
        return Ok(new { updated = true });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var claimValue = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return claimValue is not null && Guid.TryParse(claimValue, out userId);
    }
}
