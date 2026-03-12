namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Stores outgoing messages that could not be delivered because the device was offline.
/// Messages are flushed to the server when connectivity is restored.
/// </summary>
public interface IPendingMessageQueue
{
    /// <summary>Adds an outgoing message to the persistent queue.</summary>
    Task EnqueueAsync(Guid channelId, string content, CancellationToken ct = default);

    /// <summary>
    /// Returns all pending messages ordered by enqueue time (oldest first).
    /// </summary>
    Task<IReadOnlyList<PendingMessage>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Removes successfully delivered messages from the queue.</summary>
    Task RemoveAsync(IEnumerable<long> rowIds, CancellationToken ct = default);

    /// <summary>Returns the total number of messages currently queued.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>A queued outgoing message waiting to be delivered to the server.</summary>
/// <param name="RowId">Database row identifier (used for deletion after delivery).</param>
/// <param name="ChannelId">Target channel.</param>
/// <param name="Content">Message body text.</param>
/// <param name="EnqueuedAt">When the message was added to the queue (UTC).</param>
public sealed record PendingMessage(
    long RowId,
    Guid ChannelId,
    string Content,
    DateTimeOffset EnqueuedAt);
