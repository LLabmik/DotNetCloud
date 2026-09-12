using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Data.Entities.Admin;

/// <summary>
/// An administrator-authored message broadcast to every logged-in user
/// (e.g. a warning about an upcoming server reboot).
/// </summary>
/// <remarks>
/// Broadcasts are delivered as a dismissible modal dialog to connected Blazor
/// clients via SignalR and remain visible to users who log in later until the
/// broadcast expires (see <see cref="ExpiresAtUtc"/>) or an administrator
/// deletes it. Deleting a broadcast cascades to its
/// <see cref="AdminBroadcastDismissal"/> rows.
/// </remarks>
public sealed class AdminBroadcast
{
    /// <summary>Unique identifier for this broadcast.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Short headline shown in the modal header.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Body text of the message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Severity, which drives the modal's icon and styling.</summary>
    public AdminBroadcastSeverity Severity { get; set; } = AdminBroadcastSeverity.Info;

    /// <summary>The administrator who created the broadcast.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>When the broadcast was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the broadcast should be delivered, or <see langword="null"/> when it was
    /// delivered immediately on creation.
    /// </summary>
    public DateTime? ScheduledForUtc { get; set; }

    /// <summary>
    /// When the broadcast was actually delivered, or <see langword="null"/> while it is
    /// still waiting for its scheduled time.
    /// </summary>
    public DateTime? SentAtUtc { get; set; }

    /// <summary>
    /// When the broadcast stops being shown, or <see langword="null"/> for no expiry.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether the broadcast has been delivered to clients.
    /// </summary>
    public bool IsSent => SentAtUtc.HasValue;

    /// <summary>
    /// Whether the broadcast is still waiting for its scheduled delivery time.
    /// </summary>
    public bool IsPending => !SentAtUtc.HasValue;

    /// <summary>
    /// Whether the broadcast has passed its expiry time.
    /// </summary>
    public bool IsExpired(DateTime utcNow) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= utcNow;
}
