using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Maps Direct Message channels to their peer's presence state for presence-dot rendering.
/// Group channels are intentionally excluded — a presence dot represents a single user.
/// </summary>
/// <remarks>
/// Shared by the chat sidebar and the Home-page Chat widget so both attribute a DM dot to the
/// same peer and resolve the same canonical 4-state status strings.
/// </remarks>
public static class ChannelPresenceMapping
{
    /// <summary>
    /// Builds a channel-ID → peer-user-ID map for the Direct Message channels that have a known
    /// peer. Non-DM channels (Group, Public, Private) and DMs without a resolved peer are omitted
    /// so callers can render them without an attributed presence dot.
    /// </summary>
    /// <param name="channels">The channels to inspect.</param>
    /// <returns>The channel-ID → peer-user-ID map.</returns>
    public static Dictionary<Guid, Guid> BuildDmPeerMap(IEnumerable<ChannelDto> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        var map = new Dictionary<Guid, Guid>();
        foreach (var channel in channels)
        {
            if (channel.Type == "DirectMessage" && channel.OtherUserId is { } peerId)
            {
                map[channel.Id] = peerId;
            }
        }

        return map;
    }

    /// <summary>
    /// Builds a channel-ID → canonical presence status string map for the peers in
    /// <paramref name="dmPeerMap"/>. Channels whose peer has no reported state are omitted;
    /// callers render those as Offline.
    /// </summary>
    /// <param name="dmPeerMap">The channel-ID → peer-user-ID map from <see cref="BuildDmPeerMap"/>.</param>
    /// <param name="presenceStates">The peer-user-ID → presence-state lookup.</param>
    /// <returns>The channel-ID → presence status string map.</returns>
    public static Dictionary<Guid, string> BuildPresenceMap(
        IReadOnlyDictionary<Guid, Guid> dmPeerMap,
        IReadOnlyDictionary<Guid, PresenceState> presenceStates)
    {
        ArgumentNullException.ThrowIfNull(dmPeerMap);
        ArgumentNullException.ThrowIfNull(presenceStates);

        var map = new Dictionary<Guid, string>();
        foreach (var (channelId, peerId) in dmPeerMap)
        {
            if (presenceStates.TryGetValue(peerId, out var state))
            {
                map[channelId] = PresenceStatusHelpers.ToStatusString(state);
            }
        }

        return map;
    }
}
