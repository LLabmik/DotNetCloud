namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Thread-safe cache of per-channel mute state.
/// Populated when channels are loaded from the server; consulted by notification handlers
/// to decide whether to suppress alerts for muted channels.
/// </summary>
public interface IChannelMuteStateService
{
    /// <summary>Returns <c>true</c> if the channel is muted.</summary>
    bool IsMuted(Guid channelId);

    /// <summary>Updates the mute state for a channel.</summary>
    void SetMuted(Guid channelId, bool isMuted);

    /// <summary>Replaces all entries with the given set (called on channel list refresh).</summary>
    void ReplaceAll(IReadOnlyDictionary<Guid, bool> states);
}
