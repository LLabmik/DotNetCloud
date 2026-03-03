using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Manages gRPC channels for communication with module processes.
/// Provides connection pooling, automatic reconnection, and timeout handling.
/// </summary>
internal sealed class GrpcChannelManager : IDisposable
{
    private readonly ILogger<GrpcChannelManager> _logger;
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private bool _disposed;

    public GrpcChannelManager(ILogger<GrpcChannelManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a gRPC channel for a module endpoint.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="endpoint">The gRPC endpoint (e.g., "unix:///run/dotnetcloud/files.sock").</param>
    /// <returns>The gRPC channel.</returns>
    public GrpcChannel GetOrCreateChannel(string moduleId, string endpoint)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GrpcChannelManager));

        return _channels.GetOrAdd(moduleId, _ =>
        {
            var channel = CreateChannel(endpoint);
            _logger.LogDebug("Created gRPC channel for module {ModuleId} to {Endpoint}", moduleId, endpoint);
            return channel;
        });
    }

    /// <summary>
    /// Removes and disposes the channel for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    public async Task RemoveChannelAsync(string moduleId)
    {
        if (_channels.TryRemove(moduleId, out var channel))
        {
            await channel.ShutdownAsync();
            channel.Dispose();
            _logger.LogDebug("Removed gRPC channel for module {ModuleId}", moduleId);
        }
    }

    /// <summary>
    /// Gets the default call options with timeout.
    /// </summary>
    /// <param name="timeout">Optional custom timeout, or null for default.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Call options with deadline and cancellation token.</returns>
    public CallOptions GetCallOptions(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.Add(timeout ?? _defaultTimeout);
        return new CallOptions(deadline: deadline, cancellationToken: cancellationToken);
    }

    private GrpcChannel CreateChannel(string endpoint)
    {
        // Configure channel options
        var channelOptions = new GrpcChannelOptions
        {
            // Disable TLS for local IPC (Unix sockets, Named Pipes)
            UnsafeUseInsecureChannelCallCredentials = endpoint.StartsWith("unix://") ||
                                                        endpoint.StartsWith("net.pipe://"),

            // Connection settings
            MaxReceiveMessageSize = 16 * 1024 * 1024, // 16 MB
            MaxSendMessageSize = 16 * 1024 * 1024, // 16 MB

            // HTTP/2 settings
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                EnableMultipleHttp2Connections = false
            }
        };

        // Handle Unix domain socket connections
        if (endpoint.StartsWith("unix://"))
        {
            var socketPath = endpoint.Substring("unix://".Length);
            channelOptions.HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);

                    var endPoint = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
                    await socket.ConnectAsync(endPoint, cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
            };

            // Use a placeholder URL for Unix sockets (actual connection via ConnectCallback)
            endpoint = "http://localhost";
        }

        return GrpcChannel.ForAddress(endpoint, channelOptions);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var channel in _channels.Values)
        {
            try
            {
                channel.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
                channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing gRPC channel");
            }
        }

        _channels.Clear();
        _disposed = true;
    }
}
