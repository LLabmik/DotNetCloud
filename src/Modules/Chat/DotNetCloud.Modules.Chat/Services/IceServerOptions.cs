namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Configuration options for ICE (STUN/TURN) servers used by WebRTC connections.
/// Bound from the "Chat:IceServers" configuration section.
/// </summary>
public sealed class IceServerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Chat:IceServers";

    /// <summary>
    /// Whether the built-in STUN server is enabled. Defaults to <c>true</c>.
    /// When enabled, the server listens on <see cref="StunPort"/> for STUN binding requests (RFC 5389).
    /// </summary>
    public bool EnableBuiltInStun { get; set; } = true;

    /// <summary>
    /// UDP port for the built-in STUN server. Defaults to 3478 (standard STUN port).
    /// </summary>
    public int StunPort { get; set; } = 3478;

    /// <summary>
    /// Public hostname or IP address for the built-in STUN server URL advertised to clients.
    /// When empty, the server's configured hostname is used automatically.
    /// </summary>
    public string StunPublicHost { get; set; } = string.Empty;

    /// <summary>
    /// Additional STUN servers to include in the ICE server list (e.g., "stun:stun.l.google.com:19302").
    /// These are appended after the built-in STUN server. Empty by default — no third-party STUN.
    /// </summary>
    public string[] AdditionalStunUrls { get; set; } = [];

    /// <summary>
    /// Whether a TURN relay server is configured. Defaults to <c>false</c>.
    /// </summary>
    public bool EnableTurn { get; set; }

    /// <summary>
    /// TURN server URLs (e.g., "turn:turn.example.com:3478", "turns:turn.example.com:5349").
    /// </summary>
    public string[] TurnUrls { get; set; } = [];

    /// <summary>
    /// Static TURN username. Used only if <see cref="EnableEphemeralCredentials"/> is <c>false</c>.
    /// </summary>
    public string TurnUsername { get; set; } = string.Empty;

    /// <summary>
    /// Static TURN credential. Used only if <see cref="EnableEphemeralCredentials"/> is <c>false</c>.
    /// </summary>
    public string TurnCredential { get; set; } = string.Empty;

    /// <summary>
    /// Whether to generate time-limited HMAC-based credentials for TURN (coturn ephemeral credentials).
    /// When enabled, <see cref="TurnSharedSecret"/> must be set to match the coturn <c>static-auth-secret</c>.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool EnableEphemeralCredentials { get; set; }

    /// <summary>
    /// Shared secret for HMAC-SHA1 ephemeral credential generation.
    /// Must match the <c>static-auth-secret</c> configured in coturn.
    /// </summary>
    public string TurnSharedSecret { get; set; } = string.Empty;

    /// <summary>
    /// Time-to-live in seconds for ephemeral TURN credentials. Defaults to 86400 (24 hours).
    /// </summary>
    public int CredentialTtlSeconds { get; set; } = 86400;

    /// <summary>
    /// ICE transport policy sent to clients. "all" (default) uses both STUN and TURN;
    /// "relay" forces all traffic through TURN.
    /// </summary>
    public string IceTransportPolicy { get; set; } = "all";
}
