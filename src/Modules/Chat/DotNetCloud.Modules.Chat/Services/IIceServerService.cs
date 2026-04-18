namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Provides ICE server configuration for WebRTC clients.
/// Generates ephemeral TURN credentials when configured.
/// </summary>
public interface IIceServerService
{
    /// <summary>
    /// Gets the current ICE server list including STUN and optional TURN servers.
    /// TURN credentials are generated fresh per call (ephemeral when configured).
    /// </summary>
    /// <param name="publicHost">
    /// The public hostname of the server (used for the built-in STUN URL).
    /// When null, falls back to <see cref="IceServerOptions.StunPublicHost"/>.
    /// </param>
    /// <returns>A list of ICE server DTOs suitable for passing to RTCPeerConnection.</returns>
    IReadOnlyList<IceServerDto> GetIceServers(string? publicHost = null);

    /// <summary>
    /// Gets the configured ICE transport policy ("all" or "relay").
    /// </summary>
    string IceTransportPolicy { get; }
}
