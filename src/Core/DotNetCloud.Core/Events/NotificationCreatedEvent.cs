using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised after an in-app notification has been persisted. Used to fan the
/// notification out to additional delivery channels (real-time, push, email).
/// </summary>
public sealed record NotificationCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The persisted notification to fan out.</summary>
    public required NotificationDto Notification { get; init; }
}
