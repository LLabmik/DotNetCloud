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
        var endpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");

        // All internal gRPC is cleartext HTTP/2 — never use TLS.
        // Coerce https:// to http:// so the transport matches the server.
        var address = endpoint;
        if (!string.IsNullOrWhiteSpace(address) &&
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            address = "http://" + address["https://".Length..];
        }

        // If no endpoint configured, use a placeholder that will fail gracefully
        if (string.IsNullOrWhiteSpace(address))
        {
            address = "http://localhost:0";
        }

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            MaxReceiveMessageSize = 16 * 1024 * 1024,
            MaxSendMessageSize = 16 * 1024 * 1024,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5),
            }
        });
    }
}
