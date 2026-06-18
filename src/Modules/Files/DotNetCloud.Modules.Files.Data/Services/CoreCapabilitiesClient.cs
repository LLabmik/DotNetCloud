using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Files.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// gRPC client for calling Core.Server's CoreCapabilities service.
/// Uses the DOTNETCLOUD_CORE_ENDPOINT environment variable (set by ProcessSupervisor)
/// to locate the core server's gRPC endpoint.
/// </summary>
internal sealed class CoreCapabilitiesClient : ICoreCapabilitiesClient, IDisposable
{
    private readonly ILogger<CoreCapabilitiesClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<CoreCapabilities.CoreCapabilitiesClient> _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreCapabilitiesClient"/> class.
    /// </summary>
    public CoreCapabilitiesClient(ILogger<CoreCapabilitiesClient> logger)
    {
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<CoreCapabilities.CoreCapabilitiesClient>(
            () => new CoreCapabilities.CoreCapabilitiesClient(_channel.Value));
    }

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            var endpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
            return !string.IsNullOrWhiteSpace(endpoint);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CleanupAdminSharedFolderAsync(
        AdminSharedFolderDeletedEvent evt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (!IsAvailable)
        {
            _logger.LogDebug("Core capabilities client unavailable — DOTNETCLOUD_CORE_ENDPOINT not set");
            return false;
        }

        try
        {
            var request = new CleanupAdminSharedFolderRequest
            {
                SharedFolderId = evt.SharedFolderId.ToString(),
                DisplayName = evt.DisplayName,
            };

            request.MountedEntries.AddRange(
                evt.MountedEntries.Select(e => new MountedEntryMessage
                {
                    RelativePath = e.RelativePath,
                    IsDirectory = e.IsDirectory,
                }));

            var callOptions = new CallOptions(
                deadline: DateTime.UtcNow.AddSeconds(30),
                cancellationToken: cancellationToken);

            var response = await _client.Value.CleanupAdminSharedFolderAsync(request, callOptions);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "Core.Server cleanup failed for shared folder {SharedFolderId}: {Error}",
                    evt.SharedFolderId, response.ErrorMessage);
                return false;
            }

            return true;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogDebug("Core.Server gRPC service unavailable");
            return false;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("Core.Server cleanup gRPC call timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Core.Server cleanup for {SharedFolderId}", evt.SharedFolderId);
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_channel.IsValueCreated)
        {
            _channel.Value.Dispose();
        }
    }

    private GrpcChannel CreateChannel()
    {
        var address = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");

        if (string.IsNullOrWhiteSpace(address))
        {
            // Return a null-like channel — calls will fail gracefully via IsAvailable check
            return GrpcChannel.ForAddress("http://localhost:0", new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    ConnectCallback = static (_, _) =>
                        ValueTask.FromException<System.IO.Stream>(
                            new InvalidOperationException("Core endpoint not configured"))
                }
            });
        }

        var channelOptions = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 16 * 1024 * 1024,
            MaxSendMessageSize = 16 * 1024 * 1024,
        };

        // Support Unix socket addresses (unix:///path/to/socket)
        if (address.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var socketPath = address["unix://".Length..];
            var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath);
            channelOptions.HttpHandler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix,
                        System.Net.Sockets.SocketType.Stream,
                        System.Net.Sockets.ProtocolType.Unspecified);
                    await socket.ConnectAsync(endpoint, cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
            };
            // Replace address with http://localhost so GrpcChannel.ForAddress
            // doesn't fail on the unix:// scheme (which it can't resolve).
            address = "http://localhost";
        }

        return GrpcChannel.ForAddress(address, channelOptions);
    }
}
