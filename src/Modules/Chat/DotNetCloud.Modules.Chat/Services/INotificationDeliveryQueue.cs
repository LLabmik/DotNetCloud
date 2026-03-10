using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Queue contract for deferred push delivery retries.
/// </summary>
internal interface INotificationDeliveryQueue
{
    /// <summary>
    /// Enqueues a retry item.
    /// </summary>
    ValueTask EnqueueAsync(QueuedPushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads queued retry items as they become available.
    /// </summary>
    IAsyncEnumerable<QueuedPushNotification> ReadAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A queued push notification retry item.
/// </summary>
internal sealed record QueuedPushNotification
{
    /// <summary>
    /// The target user identifier.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The notification payload.
    /// </summary>
    public required PushNotification Notification { get; init; }

    /// <summary>
    /// Current retry attempt number (1-based).
    /// </summary>
    public int Attempt { get; init; } = 1;

    /// <summary>
    /// Earliest UTC time the item should be retried.
    /// </summary>
    public DateTime NextAttemptUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// In-memory channel-backed queue for deferred push delivery.
/// </summary>
internal sealed class InMemoryNotificationDeliveryQueue : INotificationDeliveryQueue
{
    private readonly Channel<QueuedPushNotification> _channel = Channel.CreateUnbounded<QueuedPushNotification>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    /// <inheritdoc />
    public ValueTask EnqueueAsync(QueuedPushNotification notification, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(notification, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<QueuedPushNotification> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }
}
