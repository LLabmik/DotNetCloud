using DotNetCloud.Modules.Chat.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for WebRTC data transfer objects: <see cref="IceServerDto"/>, <see cref="WebRtcCallConfig"/>,
/// <see cref="WebRtcCallState"/>, <see cref="WebRtcPeerState"/>, and <see cref="WebRtcMediaState"/>.
/// </summary>
[TestClass]
public class WebRtcDtoTests
{
    // ═══════════════════════════════════════════════════════════
    // IceServerDto
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void IceServerDto_StunOnly_HasNoCredentials()
    {
        var server = new IceServerDto
        {
            Urls = ["stun:stun.l.google.com:19302"]
        };

        Assert.AreEqual(1, server.Urls.Length);
        Assert.AreEqual("stun:stun.l.google.com:19302", server.Urls[0]);
        Assert.IsNull(server.Username);
        Assert.IsNull(server.Credential);
    }

    [TestMethod]
    public void IceServerDto_TurnWithCredentials_HasAllProperties()
    {
        var server = new IceServerDto
        {
            Urls = ["turn:turn.example.com:3478", "turns:turn.example.com:5349"],
            Username = "testuser",
            Credential = "testpass"
        };

        Assert.AreEqual(2, server.Urls.Length);
        Assert.AreEqual("turn:turn.example.com:3478", server.Urls[0]);
        Assert.AreEqual("turns:turn.example.com:5349", server.Urls[1]);
        Assert.AreEqual("testuser", server.Username);
        Assert.AreEqual("testpass", server.Credential);
    }

    [TestMethod]
    public void IceServerDto_RecordEquality_SameValues_AreEqual()
    {
        var a = new IceServerDto { Urls = ["stun:stun.example.com"], Username = "u", Credential = "c" };
        var b = new IceServerDto { Urls = ["stun:stun.example.com"], Username = "u", Credential = "c" };

        // Record equality checks reference equality for arrays, so different instances won't be equal
        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void IceServerDto_RecordEquality_SameInstance_AreEqual()
    {
        var urls = new[] { "stun:stun.example.com" };
        var a = new IceServerDto { Urls = urls, Username = "u", Credential = "c" };
        var b = new IceServerDto { Urls = urls, Username = "u", Credential = "c" };

        Assert.AreEqual(a, b);
    }

    // ═══════════════════════════════════════════════════════════
    // WebRtcCallConfig
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void WebRtcCallConfig_MinimalConfig_HasDefaults()
    {
        var config = new WebRtcCallConfig
        {
            CallId = "call-1",
            IceServers = [new IceServerDto { Urls = ["stun:stun.example.com"] }]
        };

        Assert.AreEqual("call-1", config.CallId);
        Assert.AreEqual(1, config.IceServers.Count);
        Assert.IsNull(config.IceTransportPolicy);
    }

    [TestMethod]
    public void WebRtcCallConfig_WithRelayPolicy_ReturnsPolicy()
    {
        var config = new WebRtcCallConfig
        {
            CallId = "call-2",
            IceServers = [new IceServerDto { Urls = ["turn:turn.example.com:3478"] }],
            IceTransportPolicy = "relay"
        };

        Assert.AreEqual("relay", config.IceTransportPolicy);
    }

    [TestMethod]
    public void WebRtcCallConfig_MultipleServers_AllPreserved()
    {
        var servers = new List<IceServerDto>
        {
            new() { Urls = ["stun:stun1.example.com"] },
            new() { Urls = ["stun:stun2.example.com"] },
            new() { Urls = ["turn:turn.example.com:3478"], Username = "u", Credential = "c" }
        };

        var config = new WebRtcCallConfig
        {
            CallId = "call-3",
            IceServers = servers
        };

        Assert.AreEqual(3, config.IceServers.Count);
    }

    // ═══════════════════════════════════════════════════════════
    // WebRtcCallState
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void WebRtcCallState_DefaultValues_AreCorrect()
    {
        var state = new WebRtcCallState();

        Assert.IsNull(state.CallId);
        Assert.AreEqual(0, state.PeerCount);
        Assert.IsFalse(state.IsScreenSharing);
        Assert.IsFalse(state.HasLocalMedia);
        Assert.IsNotNull(state.Peers);
        Assert.AreEqual(0, state.Peers.Length);
    }

    [TestMethod]
    public void WebRtcCallState_PopulatedValues_AreCorrect()
    {
        var state = new WebRtcCallState
        {
            CallId = "call-123",
            PeerCount = 3,
            IsScreenSharing = true,
            HasLocalMedia = true,
            Peers = ["peer-1", "peer-2", "peer-3"]
        };

        Assert.AreEqual("call-123", state.CallId);
        Assert.AreEqual(3, state.PeerCount);
        Assert.IsTrue(state.IsScreenSharing);
        Assert.IsTrue(state.HasLocalMedia);
        Assert.AreEqual(3, state.Peers.Length);
    }

    // ═══════════════════════════════════════════════════════════
    // WebRtcPeerState
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void WebRtcPeerState_DefaultValues_ShowsNotConnected()
    {
        var state = new WebRtcPeerState();

        Assert.IsFalse(state.Exists);
        Assert.IsNull(state.ConnectionState);
        Assert.IsNull(state.IceConnectionState);
        Assert.IsNull(state.IceGatheringState);
    }

    [TestMethod]
    public void WebRtcPeerState_ConnectedPeer_ShowsAllStates()
    {
        var state = new WebRtcPeerState
        {
            Exists = true,
            ConnectionState = "connected",
            IceConnectionState = "connected",
            IceGatheringState = "complete"
        };

        Assert.IsTrue(state.Exists);
        Assert.AreEqual("connected", state.ConnectionState);
        Assert.AreEqual("connected", state.IceConnectionState);
        Assert.AreEqual("complete", state.IceGatheringState);
    }

    [TestMethod]
    public void WebRtcPeerState_FailedPeer_ShowsFailedState()
    {
        var state = new WebRtcPeerState
        {
            Exists = true,
            ConnectionState = "failed",
            IceConnectionState = "failed",
            IceGatheringState = "complete"
        };

        Assert.IsTrue(state.Exists);
        Assert.AreEqual("failed", state.ConnectionState);
    }

    [TestMethod]
    public void WebRtcPeerState_NewPeer_ShowsNewState()
    {
        var state = new WebRtcPeerState
        {
            Exists = true,
            ConnectionState = "new",
            IceConnectionState = "new",
            IceGatheringState = "new"
        };

        Assert.IsTrue(state.Exists);
        Assert.AreEqual("new", state.ConnectionState);
        Assert.AreEqual("new", state.IceConnectionState);
        Assert.AreEqual("new", state.IceGatheringState);
    }

    // ═══════════════════════════════════════════════════════════
    // WebRtcMediaState
    // ═══════════════════════════════════════════════════════════

    [TestMethod]
    public void WebRtcMediaState_DefaultValues_AllDisabled()
    {
        var state = new WebRtcMediaState();

        Assert.IsFalse(state.HasAudio);
        Assert.IsFalse(state.AudioEnabled);
        Assert.IsFalse(state.HasVideo);
        Assert.IsFalse(state.VideoEnabled);
        Assert.IsFalse(state.IsScreenSharing);
    }

    [TestMethod]
    public void WebRtcMediaState_AudioOnly_ShowsAudioOnly()
    {
        var state = new WebRtcMediaState
        {
            HasAudio = true,
            AudioEnabled = true,
            HasVideo = false,
            VideoEnabled = false,
            IsScreenSharing = false
        };

        Assert.IsTrue(state.HasAudio);
        Assert.IsTrue(state.AudioEnabled);
        Assert.IsFalse(state.HasVideo);
    }

    [TestMethod]
    public void WebRtcMediaState_VideoMuted_ShowsMutedVideo()
    {
        var state = new WebRtcMediaState
        {
            HasAudio = true,
            AudioEnabled = true,
            HasVideo = true,
            VideoEnabled = false,
            IsScreenSharing = false
        };

        Assert.IsTrue(state.HasVideo);
        Assert.IsFalse(state.VideoEnabled);
    }

    [TestMethod]
    public void WebRtcMediaState_ScreenSharing_ShowsScreenShare()
    {
        var state = new WebRtcMediaState
        {
            HasAudio = true,
            AudioEnabled = true,
            HasVideo = true,
            VideoEnabled = true,
            IsScreenSharing = true
        };

        Assert.IsTrue(state.IsScreenSharing);
    }

    [TestMethod]
    public void WebRtcMediaState_AllEnabled_ShowsFullMedia()
    {
        var state = new WebRtcMediaState
        {
            HasAudio = true,
            AudioEnabled = true,
            HasVideo = true,
            VideoEnabled = true,
            IsScreenSharing = false
        };

        Assert.IsTrue(state.HasAudio);
        Assert.IsTrue(state.AudioEnabled);
        Assert.IsTrue(state.HasVideo);
        Assert.IsTrue(state.VideoEnabled);
        Assert.IsFalse(state.IsScreenSharing);
    }
}
