namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a user's presence status.
/// </summary>
/// <param name="UserId">The user's unique identifier.</param>
/// <param name="IsOnline">Whether the user is currently online.</param>
/// <param name="LastSeenAt">The UTC timestamp of the user's last activity, or null if never seen.</param>
public sealed record UserPresenceDto(
    Guid UserId,
    bool IsOnline,
    DateTime? LastSeenAt);

/// <summary>
/// Represents a real-time message envelope sent to clients.
/// </summary>
/// <param name="EventName">The client-side event name.</param>
/// <param name="Payload">The message payload.</param>
/// <param name="SentAt">The UTC timestamp when the message was sent.</param>
/// <param name="SourceModule">The module that originated the message, if applicable.</param>
public sealed record RealtimeMessageDto(
    string EventName,
    object Payload,
    DateTime SentAt,
    string? SourceModule = null);
