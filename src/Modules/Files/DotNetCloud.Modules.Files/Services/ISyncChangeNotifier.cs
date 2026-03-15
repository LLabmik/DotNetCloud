namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Publishes and subscribes to lightweight sync-change notifications per user.
/// Used by the SSE endpoint (<c>/api/v1/files/sync/stream</c>) to push real-time
/// notifications to connected clients, replacing polling for active connections.
/// </summary>
public interface ISyncChangeNotifier
{
    /// <summary>
    /// Notifies all connected listeners for the given user that new changes are available.
    /// Called after a <c>SyncSequence</c> increment (file mutation).
    /// </summary>
    Task NotifyAsync(Guid userId, long latestSequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> that yields notifications for the given user.
    /// Each notification contains the latest sequence number. The enumerable completes when
    /// <paramref name="cancellationToken"/> is cancelled or the connection limit is exceeded.
    /// </summary>
    IAsyncEnumerable<SyncChangeNotification> SubscribeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current number of active SSE connections for a user.
    /// </summary>
    int GetConnectionCount(Guid userId);
}

/// <summary>
/// Lightweight notification pushed over SSE when a user's file tree changes.
/// </summary>
public sealed record SyncChangeNotification
{
    /// <summary>The user whose file tree changed.</summary>
    public Guid UserId { get; init; }

    /// <summary>The latest sync sequence number after the mutation.</summary>
    public long LatestSequence { get; init; }

    /// <summary>When the notification was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
