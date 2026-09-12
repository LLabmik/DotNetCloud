using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelPresenceMapping"/>, the DM peer/presence attribution shared by
/// the chat sidebar and the Home-page Chat widget.
/// </summary>
[TestClass]
public class ChannelPresenceMappingTests
{
    [TestMethod]
    public void BuildDmPeerMap_DirectMessageWithPeer_MapsChannelToPeer()
    {
        var channelId = Guid.CreateVersion7();
        var peerId = Guid.CreateVersion7();
        var channels = new[] { Dm(channelId, peerId) };

        var map = ChannelPresenceMapping.BuildDmPeerMap(channels);

        Assert.AreEqual(1, map.Count);
        Assert.AreEqual(peerId, map[channelId]);
    }

    [TestMethod]
    public void BuildDmPeerMap_GroupAndPublicChannels_AreExcluded()
    {
        var channels = new[]
        {
            new ChannelDto { Id = Guid.CreateVersion7(), Name = "Team", Type = "Group" },
            new ChannelDto { Id = Guid.CreateVersion7(), Name = "Public", Type = "Public" },
            new ChannelDto { Id = Guid.CreateVersion7(), Name = "Private", Type = "Private" }
        };

        var map = ChannelPresenceMapping.BuildDmPeerMap(channels);

        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void BuildDmPeerMap_DirectMessageWithoutResolvedPeer_IsExcluded()
    {
        var channels = new[] { new ChannelDto { Id = Guid.CreateVersion7(), Name = "DM-unknown", Type = "DirectMessage" } };

        var map = ChannelPresenceMapping.BuildDmPeerMap(channels);

        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void BuildPresenceMap_KnownStates_UsesCanonicalStatusStrings()
    {
        var onlineChannel = Guid.CreateVersion7();
        var awayChannel = Guid.CreateVersion7();
        var dndChannel = Guid.CreateVersion7();
        var onlinePeer = Guid.CreateVersion7();
        var awayPeer = Guid.CreateVersion7();
        var dndPeer = Guid.CreateVersion7();

        var dmPeerMap = new Dictionary<Guid, Guid>
        {
            [onlineChannel] = onlinePeer,
            [awayChannel] = awayPeer,
            [dndChannel] = dndPeer
        };
        var states = new Dictionary<Guid, PresenceState>
        {
            [onlinePeer] = PresenceState.Online,
            [awayPeer] = PresenceState.Away,
            [dndPeer] = PresenceState.DoNotDisturb
        };

        var map = ChannelPresenceMapping.BuildPresenceMap(dmPeerMap, states);

        Assert.AreEqual("Online", map[onlineChannel]);
        Assert.AreEqual("Away", map[awayChannel]);
        Assert.AreEqual("DoNotDisturb", map[dndChannel]);
    }

    [TestMethod]
    public void BuildPresenceMap_PeerWithoutReportedState_IsOmittedAndRendersOffline()
    {
        var knownChannel = Guid.CreateVersion7();
        var unknownChannel = Guid.CreateVersion7();
        var knownPeer = Guid.CreateVersion7();
        var unknownPeer = Guid.CreateVersion7();

        var dmPeerMap = new Dictionary<Guid, Guid>
        {
            [knownChannel] = knownPeer,
            [unknownChannel] = unknownPeer
        };
        var states = new Dictionary<Guid, PresenceState> { [knownPeer] = PresenceState.Offline };

        var map = ChannelPresenceMapping.BuildPresenceMap(dmPeerMap, states);

        Assert.IsFalse(map.ContainsKey(unknownChannel));
        Assert.AreEqual("presence-offline", PresenceStatusHelpers.GetCssClass(map.GetValueOrDefault(unknownChannel)));
    }

    private static ChannelDto Dm(Guid channelId, Guid peerId) => new()
    {
        Id = channelId,
        Name = "Peer",
        Type = "DirectMessage",
        OtherUserId = peerId
    };
}
