using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// In-memory implementation of <see cref="ISyncChangeNotifier"/> using per-user channels.
/// Supports up to <see cref="MaxConnectionsPerUser"/> concurrent SSE connections per user.
/// </summary>
internal sealed class SyncChangeNotifier : ISyncChangeNotifier
{
    /// <summary>Maximum concurrent SSE connections per user.</summary>
    internal const int MaxConnectionsPerUser = 25;

    private readonly ConcurrentDictionary<Guid, UserSubscriptions> _subscriptions = new();
    private readonly ILogger<SyncChangeNotifier> _logger;

    public SyncChangeNotifier(ILogger<SyncChangeNotifier> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(Guid userId, long latestSequence, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(userId, out var subs))
            return;

        var notification = new SyncChangeNotification
        {
            UserId = userId,
            LatestSequence = latestSequence,
        };

        var channels = subs.GetChannels();
        foreach (var channel in channels)
        {
            try
            {
                // TryWrite is non-blocking. If the channel is full (slow consumer), the notification is dropped.
                // The client will catch up on the next poll or the next notification.
                channel.Writer.TryWrite(notification);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to write notification to channel for user {UserId}.", userId);
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SyncChangeNotification> SubscribeAsync(
        Guid userId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subs = _subscriptions.GetOrAdd(userId, _ => new UserSubscriptions());

        // Bounded channel: drop oldest if consumer is slow (prevents memory leak)
        var channel = Channel.CreateBounded<SyncChangeNotification>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        if (!subs.TryAdd(channel))
        {
            _logger.LogWarning(
                "SSE connection limit ({Max}) reached for user {UserId}. Rejecting new connection.",
                MaxConnectionsPerUser, userId);
            yield break;
        }

        _logger.LogInformation("SSE connection opened for user {UserId}. Active: {Count}.",
            userId, subs.Count);

        try
        {
            await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return notification;
            }
        }
        finally
        {
            subs.Remove(channel);
            channel.Writer.TryComplete();

            if (subs.Count == 0)
                _subscriptions.TryRemove(userId, out _);

            _logger.LogInformation("SSE connection closed for user {UserId}. Active: {Count}.",
                userId, subs.Count);
        }
    }

    /// <inheritdoc />
    public int GetConnectionCount(Guid userId)
    {
        return _subscriptions.TryGetValue(userId, out var subs) ? subs.Count : 0;
    }

    /// <summary>
    /// Thread-safe collection of channels for a single user's SSE connections.
    /// </summary>
    private sealed class UserSubscriptions
    {
        private readonly object _lock = new();
        private readonly List<Channel<SyncChangeNotification>> _channels = [];

        public int Count
        {
            get { lock (_lock) { return _channels.Count; } }
        }

        public bool TryAdd(Channel<SyncChangeNotification> channel)
        {
            lock (_lock)
            {
                if (_channels.Count >= MaxConnectionsPerUser)
                    return false;
                _channels.Add(channel);
                return true;
            }
        }

        public void Remove(Channel<SyncChangeNotification> channel)
        {
            lock (_lock) { _channels.Remove(channel); }
        }

        public Channel<SyncChangeNotification>[] GetChannels()
        {
            lock (_lock) { return [.. _channels]; }
        }
    }
}
