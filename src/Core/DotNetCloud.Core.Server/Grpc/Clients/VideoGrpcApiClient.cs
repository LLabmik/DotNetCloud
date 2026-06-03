using DotNetCloud.Modules.Video.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Video gRPC client used by the Core Server.
/// </summary>
public sealed class VideoGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "VideoGrpc";
    /// <summary>The gRPC address of the Video module.</summary>
    public string VideoModuleAddress { get; set; } = "http://localhost:5007";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IVideoApiClient"/>.
/// </summary>
public sealed class VideoGrpcApiClient : IVideoApiClient, IDisposable
{
    private readonly VideoGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<VideoGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="VideoGrpcApiClient"/> class.</summary>
    public VideoGrpcApiClient(
        IOptions<VideoGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<VideoGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.video");
        _logger.LogInformation("VideoGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
