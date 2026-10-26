using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Photos gRPC client used by the Core Server.
/// </summary>
public sealed class PhotosGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "PhotosGrpc";
    /// <summary>The gRPC address of the Photos module.</summary>
    public string PhotosModuleAddress { get; set; } = "http://localhost:5006";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IPhotosApiClient"/>.
/// </summary>
public sealed class PhotosGrpcApiClient : IPhotosApiClient, IDisposable
{
    private readonly PhotosGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly Supervisor.IModuleGrpcChannelProvider _channelManager;
    private readonly ILogger<PhotosGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="PhotosGrpcApiClient"/> class.</summary>
    public PhotosGrpcApiClient(
        IOptions<PhotosGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        Supervisor.IModuleGrpcChannelProvider channelManager,
        ILogger<PhotosGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _channelManager = channelManager;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.photos");
        _logger.LogInformation("PhotosGrpcApiClient connecting to {Address}", address);
        // Managed channel: it attaches the supervisor-issued credential the module host requires.
        return _channelManager.GetOrCreateChannel("dotnetcloud.photos", address);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The channel belongs to GrpcChannelManager and is shared, so there is nothing to release.
        if (_disposed) return;
        _disposed = true;
    }
}
