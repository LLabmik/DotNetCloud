using System.Collections.Concurrent;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// In-memory typing indicator service with time-based expiration.
/// Tracks which users are currently typing in each channel.
/// </summary>
internal sealed class TypingIndicatorService : ITypingIndicatorService
{
    private static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, TypingEntry>> _channelTyping = new();

    /// <inheritdoc />
    public Task NotifyTypingAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (channelId == Guid.Empty)
            throw new ArgumentException("Channel id is required.", nameof(channelId));

        ArgumentNullException.ThrowIfNull(caller);

        var channelDict = _channelTyping.GetOrAdd(channelId, _ => new ConcurrentDictionary<Guid, TypingEntry>());
        channelDict[caller.UserId] = new TypingEntry(DateTime.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TypingIndicatorDto>> GetTypingUsersAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (channelId == Guid.Empty)
            throw new ArgumentException("Channel id is required.", nameof(channelId));

        if (!_channelTyping.TryGetValue(channelId, out var channelDict))
        {
            return Task.FromResult<IReadOnlyList<TypingIndicatorDto>>([]);
        }

        var now = DateTime.UtcNow;
        var result = new List<TypingIndicatorDto>();

        foreach (var kvp in channelDict)
        {
            if (now - kvp.Value.Timestamp < TypingTimeout)
            {
                result.Add(new TypingIndicatorDto
                {
                    ChannelId = channelId,
                    UserId = kvp.Key
                });
            }
            else
            {
                channelDict.TryRemove(kvp.Key, out _);
            }
        }

        if (channelDict.IsEmpty)
        {
            _channelTyping.TryRemove(channelId, out _);
        }

        return Task.FromResult<IReadOnlyList<TypingIndicatorDto>>(result);
    }

    /// <summary>
    /// Removes expired typing entries across all channels.
    /// Called periodically by background cleanup.
    /// </summary>
    internal void CleanupExpired()
    {
        var now = DateTime.UtcNow;

        foreach (var channelKvp in _channelTyping)
        {
            foreach (var userKvp in channelKvp.Value)
            {
                if (now - userKvp.Value.Timestamp >= TypingTimeout)
                {
                    channelKvp.Value.TryRemove(userKvp.Key, out _);
                }
            }

            if (channelKvp.Value.IsEmpty)
            {
                _channelTyping.TryRemove(channelKvp.Key, out _);
            }
        }
    }

    private sealed record TypingEntry(DateTime Timestamp);
}
