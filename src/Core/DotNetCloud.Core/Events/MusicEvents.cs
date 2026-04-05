namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a track is played.
/// </summary>
public sealed record TrackPlayedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the track that was played.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>The ID of the user who played the track.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Duration the track was played in seconds.</summary>
    public int DurationPlayedSeconds { get; init; }
}

/// <summary>
/// Raised when a playlist is created.
/// </summary>
public sealed record PlaylistCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the newly created playlist.</summary>
    public required Guid PlaylistId { get; init; }

    /// <summary>The playlist name.</summary>
    public required string Name { get; init; }

    /// <summary>The ID of the user who created the playlist.</summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when a library scan completes.
/// </summary>
public sealed record LibraryScanCompletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the user whose library was scanned.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Number of new tracks discovered.</summary>
    public int TracksAdded { get; init; }

    /// <summary>Number of tracks updated with new metadata.</summary>
    public int TracksUpdated { get; init; }

    /// <summary>Number of tracks removed (files no longer exist).</summary>
    public int TracksRemoved { get; init; }
}

/// <summary>
/// Raised when a track is scrobbled (play completion recorded).
/// </summary>
public sealed record TrackScrobbledEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the scrobbled track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>The ID of the user who scrobbled the track.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The artist name at time of scrobble.</summary>
    public required string ArtistName { get; init; }

    /// <summary>The track title at time of scrobble.</summary>
    public required string TrackTitle { get; init; }

    /// <summary>The album title at time of scrobble.</summary>
    public string? AlbumTitle { get; init; }
}
