namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Severity of an administrator broadcast, which controls how prominently the
/// message is presented to users.
/// </summary>
public enum AdminBroadcastSeverity
{
    /// <summary>Informational message (routine announcements).</summary>
    Info = 0,

    /// <summary>Warning about an upcoming disruptive event (e.g. a server reboot).</summary>
    Warning = 1,

    /// <summary>Critical, imminent-impact message (e.g. immediate maintenance).</summary>
    Critical = 2,
}

/// <summary>
/// Lifecycle status of an administrator broadcast as shown in the admin history list.
/// </summary>
public enum AdminBroadcastStatus
{
    /// <summary>Waiting for its scheduled delivery time.</summary>
    Scheduled = 0,

    /// <summary>Delivered and still within its validity window.</summary>
    Sent = 1,

    /// <summary>Delivered but past its expiry time.</summary>
    Expired = 2,
}

/// <summary>
/// Administrative view of a broadcast, used by the admin history list.
/// </summary>
public sealed record AdminBroadcastDto
{
    /// <summary>Unique identifier for the broadcast.</summary>
    public required Guid Id { get; init; }

    /// <summary>Short headline shown in the modal header.</summary>
    public required string Title { get; init; }

    /// <summary>Body text of the message.</summary>
    public required string Message { get; init; }

    /// <summary>Severity, which drives the modal's icon and styling.</summary>
    public required AdminBroadcastSeverity Severity { get; init; }

    /// <summary>Derived lifecycle status.</summary>
    public required AdminBroadcastStatus Status { get; init; }

    /// <summary>The administrator who created the broadcast.</summary>
    public required Guid CreatedByUserId { get; init; }

    /// <summary>When the broadcast was created (UTC).</summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>When the broadcast is scheduled for delivery, if it was not sent immediately.</summary>
    public DateTime? ScheduledForUtc { get; init; }

    /// <summary>When the broadcast was delivered, or <see langword="null"/> while still scheduled.</summary>
    public DateTime? SentAtUtc { get; init; }

    /// <summary>When the broadcast stops being shown, or <see langword="null"/> for no expiry.</summary>
    public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>How many users have dismissed the broadcast.</summary>
    public int DismissedCount { get; init; }
}

/// <summary>
/// Request to create (and immediately or later deliver) an administrator broadcast.
/// </summary>
public sealed record CreateAdminBroadcastRequest
{
    /// <summary>Short headline shown in the modal header.</summary>
    public required string Title { get; init; }

    /// <summary>Body text of the message.</summary>
    public required string Message { get; init; }

    /// <summary>Severity, which drives the modal's icon and styling.</summary>
    public AdminBroadcastSeverity Severity { get; init; } = AdminBroadcastSeverity.Info;

    /// <summary>
    /// When to deliver the broadcast. When <see langword="null"/> or in the past, the
    /// broadcast is delivered immediately.
    /// </summary>
    public DateTime? ScheduledForUtc { get; init; }

    /// <summary>When the broadcast stops being shown, or <see langword="null"/> for no expiry.</summary>
    public DateTime? ExpiresAtUtc { get; init; }
}

/// <summary>
/// Payload sent to clients over SignalR and returned by the active-broadcast endpoint.
/// </summary>
public sealed record ActiveAdminBroadcastDto
{
    /// <summary>Unique identifier for the broadcast.</summary>
    public required Guid Id { get; init; }

    /// <summary>Short headline shown in the modal header.</summary>
    public required string Title { get; init; }

    /// <summary>Body text of the message.</summary>
    public required string Message { get; init; }

    /// <summary>Severity, which drives the modal's icon and styling.</summary>
    public required AdminBroadcastSeverity Severity { get; init; }

    /// <summary>When the broadcast was delivered (UTC).</summary>
    public required DateTime SentAtUtc { get; init; }

    /// <summary>When the broadcast stops being shown, or <see langword="null"/> for no expiry.</summary>
    public DateTime? ExpiresAtUtc { get; init; }
}
