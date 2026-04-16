namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Represents an ICE server configuration for WebRTC connections.
/// </summary>
public sealed record IceServerDto
{
    /// <summary>
    /// One or more STUN/TURN server URLs (e.g., "stun:stun.l.google.com:19302", "turn:turn.example.com:3478").
    /// </summary>
    public required string[] Urls { get; init; }

    /// <summary>
    /// TURN username (null for STUN-only servers).
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// TURN credential (null for STUN-only servers).
    /// </summary>
    public string? Credential { get; init; }
}

/// <summary>
/// Configuration passed to the client-side WebRTC engine on call initialization.
/// </summary>
public sealed record WebRtcCallConfig
{
    /// <summary>
    /// The call identifier.
    /// </summary>
    public required string CallId { get; init; }

    /// <summary>
    /// ICE server list (STUN + TURN).
    /// </summary>
    public required IReadOnlyList<IceServerDto> IceServers { get; init; }

    /// <summary>
    /// ICE transport policy: "all" (default) or "relay" (force TURN).
    /// </summary>
    public string? IceTransportPolicy { get; init; }
}

/// <summary>
/// Represents the call state returned from the JS WebRTC engine.
/// </summary>
public sealed record WebRtcCallState
{
    /// <summary>
    /// The current call ID, or null if no call is active.
    /// </summary>
    public string? CallId { get; init; }

    /// <summary>
    /// Number of active peer connections.
    /// </summary>
    public int PeerCount { get; init; }

    /// <summary>
    /// Whether screen sharing is active.
    /// </summary>
    public bool IsScreenSharing { get; init; }

    /// <summary>
    /// Whether local media has been acquired.
    /// </summary>
    public bool HasLocalMedia { get; init; }

    /// <summary>
    /// List of connected peer user IDs.
    /// </summary>
    public string[] Peers { get; init; } = [];
}

/// <summary>
/// Represents a peer's connection state returned from the JS WebRTC engine.
/// </summary>
public sealed record WebRtcPeerState
{
    /// <summary>
    /// Whether the peer connection exists.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// RTCPeerConnection.connectionState value.
    /// </summary>
    public string? ConnectionState { get; init; }

    /// <summary>
    /// RTCPeerConnection.iceConnectionState value.
    /// </summary>
    public string? IceConnectionState { get; init; }

    /// <summary>
    /// RTCPeerConnection.iceGatheringState value.
    /// </summary>
    public string? IceGatheringState { get; init; }
}

/// <summary>
/// Represents the local media track state.
/// </summary>
public sealed record WebRtcMediaState
{
    /// <summary>
    /// Whether an audio track exists.
    /// </summary>
    public bool HasAudio { get; init; }

    /// <summary>
    /// Whether the audio track is enabled (unmuted).
    /// </summary>
    public bool AudioEnabled { get; init; }

    /// <summary>
    /// Whether a video track exists.
    /// </summary>
    public bool HasVideo { get; init; }

    /// <summary>
    /// Whether the video track is enabled.
    /// </summary>
    public bool VideoEnabled { get; init; }

    /// <summary>
    /// Whether screen sharing is active.
    /// </summary>
    public bool IsScreenSharing { get; init; }
}
