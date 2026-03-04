using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.UI.Android.Services;

/// <summary>
/// Provides offline storage for messages and channels.
/// Caches data locally so the app remains functional without connectivity.
/// </summary>
public interface IOfflineStorageService
{
    /// <summary>Stores messages locally for offline access.</summary>
    Task CacheMessagesAsync(Guid channelId, IReadOnlyList<MessageDto> messages, CancellationToken cancellationToken = default);

    /// <summary>Gets cached messages for a channel.</summary>
    Task<IReadOnlyList<MessageDto>> GetCachedMessagesAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Stores channels locally for offline access.</summary>
    Task CacheChannelsAsync(IReadOnlyList<ChannelDto> channels, CancellationToken cancellationToken = default);

    /// <summary>Gets cached channels.</summary>
    Task<IReadOnlyList<ChannelDto>> GetCachedChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Queues a message for sending when connectivity is restored.</summary>
    Task QueuePendingMessageAsync(Guid channelId, string content, CancellationToken cancellationToken = default);

    /// <summary>Gets pending messages to send.</summary>
    Task<IReadOnlyList<PendingMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a pending message after successful send.</summary>
    Task RemovePendingMessageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Clears all cached data.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A message queued for sending when connectivity is restored.
/// </summary>
public sealed record PendingMessage
{
    /// <summary>Local ID for tracking.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Target channel.</summary>
    public Guid ChannelId { get; init; }

    /// <summary>Message content.</summary>
    public required string Content { get; init; }

    /// <summary>When the message was queued.</summary>
    public DateTime QueuedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// In-memory offline storage service for initial skeleton.
/// Production would use SQLite or similar.
/// </summary>
internal sealed class OfflineStorageService : IOfflineStorageService
{
    private readonly Dictionary<Guid, List<MessageDto>> _messageCache = [];
    private readonly List<ChannelDto> _channelCache = [];
    private readonly List<PendingMessage> _pendingMessages = [];

    /// <inheritdoc />
    public Task CacheMessagesAsync(Guid channelId, IReadOnlyList<MessageDto> messages, CancellationToken cancellationToken = default)
    {
        _messageCache[channelId] = [.. messages];
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MessageDto>> GetCachedMessagesAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MessageDto>>(
            _messageCache.TryGetValue(channelId, out var msgs) ? msgs : []);
    }

    /// <inheritdoc />
    public Task CacheChannelsAsync(IReadOnlyList<ChannelDto> channels, CancellationToken cancellationToken = default)
    {
        _channelCache.Clear();
        _channelCache.AddRange(channels);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChannelDto>> GetCachedChannelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ChannelDto>>(_channelCache);
    }

    /// <inheritdoc />
    public Task QueuePendingMessageAsync(Guid channelId, string content, CancellationToken cancellationToken = default)
    {
        _pendingMessages.Add(new PendingMessage { ChannelId = channelId, Content = content });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PendingMessage>>(_pendingMessages);
    }

    /// <inheritdoc />
    public Task RemovePendingMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _pendingMessages.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _messageCache.Clear();
        _channelCache.Clear();
        _pendingMessages.Clear();
        return Task.CompletedTask;
    }
}
