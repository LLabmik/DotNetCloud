namespace DotNetCloud.Core.Events;

/// <summary>
/// Event raised when a user comes online (first connection established).
/// </summary>
public sealed record UserConnectedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the user who connected.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The SignalR connection ID.
    /// </summary>
    public required string ConnectionId { get; init; }
}

/// <summary>
/// Event raised when a user goes offline (last connection dropped).
/// </summary>
public sealed record UserDisconnectedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the user who disconnected.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The SignalR connection ID that was lost.
    /// </summary>
    public required string ConnectionId { get; init; }

    /// <summary>
    /// The UTC timestamp of the user's last activity before disconnecting.
    /// </summary>
    public required DateTime LastSeenAt { get; init; }
}
