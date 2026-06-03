using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Tracks gRPC client used by the Core Server.
/// </summary>
public sealed class TracksGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "TracksGrpc";
    /// <summary>The gRPC address of the Tracks module.</summary>
    public string TracksModuleAddress { get; set; } = "http://localhost:5011";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC client for the Tracks module.
/// NOTE: Implements IDisposable only — interface methods not yet implemented.
/// Actual RPC calls will be added when proto methods are defined.
/// </summary>
public sealed class TracksGrpcApiClient : IDisposable
{
    private readonly TracksGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<TracksGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="TracksGrpcApiClient"/> class.</summary>
    public TracksGrpcApiClient(
        IOptions<TracksGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<TracksGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.tracks");
        _logger.LogInformation("TracksGrpcApiClient connecting to {Address}", address);
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
