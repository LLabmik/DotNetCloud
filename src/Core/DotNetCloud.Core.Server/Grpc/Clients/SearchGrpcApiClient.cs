using DotNetCloud.Modules.Search.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Search gRPC client used by the Core Server.
/// </summary>
public sealed class SearchGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "SearchGrpc";
    /// <summary>The gRPC address of the Search module.</summary>
    public string SearchModuleAddress { get; set; } = "http://localhost:5008";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="ISearchApiClient"/>.
/// </summary>
public sealed class SearchGrpcApiClient : ISearchApiClient, IDisposable
{
    private readonly SearchGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<SearchGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SearchGrpcApiClient"/> class.</summary>
    public SearchGrpcApiClient(
        IOptions<SearchGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<SearchGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.search");
        _logger.LogInformation("SearchGrpcApiClient connecting to {Address}", address);
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
