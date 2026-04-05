namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a video is added to the library.
/// </summary>
public sealed record VideoAddedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the video record.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The FileNode ID the video references.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The ID of the user who owns the video.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The original filename.</summary>
    public required string FileName { get; init; }
}

/// <summary>
/// Raised when a video is deleted.
/// </summary>
public sealed record VideoDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the video that was deleted.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The ID of the user who owned the video.</summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when a video is watched (play completed or significant progress).
/// </summary>
public sealed record VideoWatchedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the video that was watched.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The ID of the user who watched the video.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Duration watched in seconds.</summary>
    public int DurationWatchedSeconds { get; init; }

    /// <summary>Position in ticks where the user stopped watching.</summary>
    public long PositionTicks { get; init; }
}
