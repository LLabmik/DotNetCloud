using System.Collections.Concurrent;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Default implementation of <see cref="IChannelMuteStateService"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
internal sealed class ChannelMuteStateService : IChannelMuteStateService
{
    private readonly ConcurrentDictionary<Guid, bool> _muted = new();

    /// <inheritdoc />
    public bool IsMuted(Guid channelId) =>
        _muted.TryGetValue(channelId, out var isMuted) && isMuted;

    /// <inheritdoc />
    public void SetMuted(Guid channelId, bool isMuted) =>
        _muted[channelId] = isMuted;

    /// <inheritdoc />
    public void ReplaceAll(IReadOnlyDictionary<Guid, bool> states)
    {
        _muted.Clear();
        foreach (var kvp in states)
            _muted[kvp.Key] = kvp.Value;
    }
}
