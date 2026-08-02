namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Discriminator for the kind of outgoing operation stored in the offline queue.
/// The enum value doubles as a flush priority — lower values are delivered first.
/// </summary>
public enum OfflineOperationType
{
    /// <summary>Sending a chat message.</summary>
    ChatMessage = 0,

    /// <summary>Creating a note.</summary>
    NoteCreate = 10,

    /// <summary>Updating a note.</summary>
    NoteUpdate = 11,

    /// <summary>Deleting a note.</summary>
    NoteDelete = 12,

    /// <summary>Creating a calendar event.</summary>
    CalendarEventCreate = 20,

    /// <summary>Updating a calendar event.</summary>
    CalendarEventUpdate = 21,

    /// <summary>Deleting a calendar event.</summary>
    CalendarEventDelete = 22,
}

/// <summary>
/// A queued outgoing operation waiting to be delivered to the server.
/// </summary>
/// <param name="RowId">Database row identifier (used for deletion after delivery).</param>
/// <param name="OperationType">Type of operation, also used for flush priority.</param>
/// <param name="PayloadJson">Serialized payload specific to the operation type.</param>
/// <param name="EnqueuedAt">When the operation was added to the queue (UTC).</param>
public sealed record QueuedOperation(
    long RowId,
    OfflineOperationType OperationType,
    string PayloadJson,
    DateTimeOffset EnqueuedAt);

/// <summary>
/// Persistent store for outgoing operations that could not be delivered because the
/// device was offline. Operations are flushed to the server when connectivity returns.
/// </summary>
public interface IOfflineOperationQueue
{
    /// <summary>Adds an outgoing operation to the persistent queue.</summary>
    Task EnqueueAsync(OfflineOperationType operationType, string payloadJson, CancellationToken ct = default);

    /// <summary>Returns all queued operations ordered by enqueue time (oldest first).</summary>
    Task<IReadOnlyList<QueuedOperation>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Removes successfully delivered operations from the queue.</summary>
    Task RemoveAsync(IEnumerable<long> rowIds, CancellationToken ct = default);

    /// <summary>Returns the total number of operations currently queued.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}
