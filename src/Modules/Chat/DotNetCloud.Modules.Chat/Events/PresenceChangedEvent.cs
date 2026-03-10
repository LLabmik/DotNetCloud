using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Published when a user's chat presence state changes.
/// </summary>
public sealed record PresenceChangedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The user whose presence changed.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Presence status value (Online, Away, DoNotDisturb, Offline).</summary>
    public required string Status { get; init; }

    /// <summary>Optional custom status message.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>UTC timestamp of latest user activity.</summary>
    public DateTime? LastSeenAt { get; init; }
}
