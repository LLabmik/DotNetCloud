namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Configuration options for the DotNetCloud SignalR real-time communication infrastructure.
/// </summary>
public sealed class SignalROptions
{
    /// <summary>
    /// Configuration section key in appsettings.json.
    /// </summary>
    public const string SectionName = "SignalR";

    /// <summary>
    /// Gets or sets the interval (in seconds) at which the server sends keep-alive pings to clients.
    /// Defaults to 15 seconds.
    /// </summary>
    public int KeepAliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the timeout (in seconds) for client connections.
    /// If the server doesn't receive a message within this period, the connection is closed.
    /// Defaults to 30 seconds.
    /// </summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the timeout (in seconds) for handshake completion.
    /// If the client doesn't send an initial handshake message within this time, the connection is closed.
    /// Defaults to 15 seconds.
    /// </summary>
    public int HandshakeTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Gets or sets the maximum number of hub method invocations that can be buffered
    /// before backpressure is applied. Defaults to 10.
    /// </summary>
    public int MaximumParallelInvocationsPerClient { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum message size (in bytes) the hub can receive from a client.
    /// Defaults to 32 KB.
    /// </summary>
    public int MaximumReceiveMessageSize { get; set; } = 32 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections allowed.
    /// Zero means unlimited. Defaults to 0 (unlimited).
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Gets or sets the hub endpoint path. Defaults to "/hubs/core".
    /// </summary>
    public string HubPath { get; set; } = "/hubs/core";

    /// <summary>
    /// Gets or sets whether to enable detailed error messages sent to clients.
    /// Should be disabled in production. Defaults to false.
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// Gets or sets the WebSocket keep-alive interval in seconds.
    /// Defaults to 30 seconds. Set to 0 to disable.
    /// </summary>
    public int WebSocketKeepAliveSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to enable the WebSocket transport. Defaults to true.
    /// </summary>
    public bool EnableWebSockets { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the Server-Sent Events transport. Defaults to true.
    /// </summary>
    public bool EnableServerSentEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the Long Polling transport. Defaults to true.
    /// </summary>
    public bool EnableLongPolling { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval (in seconds) for periodic presence cleanup of stale connections.
    /// Defaults to 60 seconds.
    /// </summary>
    public int PresenceCleanupIntervalSeconds { get; set; } = 60;
}
