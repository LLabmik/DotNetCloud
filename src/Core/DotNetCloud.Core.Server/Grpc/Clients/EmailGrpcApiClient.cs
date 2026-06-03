using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Email gRPC client used by the Core Server.
/// </summary>
public sealed class EmailGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "EmailGrpc";
    /// <summary>The gRPC address of the Email module.</summary>
    public string EmailModuleAddress { get; set; } = "http://localhost:5013";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC client for the Email module.
/// NOTE: Implements IDisposable only — interface methods not yet implemented.
/// Actual RPC calls will be added when proto methods are defined.
/// </summary>
public sealed class EmailGrpcApiClient : IDisposable
{
    private readonly EmailGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<EmailGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="EmailGrpcApiClient"/> class.</summary>
    public EmailGrpcApiClient(
        IOptions<EmailGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<EmailGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.email");
        _logger.LogInformation("EmailGrpcApiClient connecting to {Address}", address);
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
