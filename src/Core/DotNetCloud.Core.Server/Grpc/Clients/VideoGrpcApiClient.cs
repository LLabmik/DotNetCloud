using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Video.Host.Protos;
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
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    /// <inheritdoc />
    public async Task<WatchProgressDto?> GetWatchProgressAsync(Guid videoId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new VideoGrpcService.VideoGrpcServiceClient(_channel.Value);
            var request = new GetWatchProgressRequest
            {
                VideoId = videoId.ToString(),
                UserId = userId.ToString()
            };

            var response = await client.GetWatchProgressAsync(request,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            if (!response.Success || response.Progress is null)
                return null;

            return new WatchProgressDto
            {
                VideoId = Guid.Parse(response.Progress.VideoId),
                VideoTitle = string.Empty,
                PositionTicks = response.Progress.PositionTicks,
                DurationTicks = 0,
                ProgressPercent = 0,
                LastWatchedAt = DateTime.TryParse(response.Progress.UpdatedAt, out var dt) ? dt : DateTime.UtcNow
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(ex, "Video module is not available for GetWatchProgress");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get watch progress via gRPC");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateWatchProgressAsync(Guid videoId, long positionTicks, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new VideoGrpcService.VideoGrpcServiceClient(_channel.Value);
            var request = new UpdateWatchProgressRequest
            {
                VideoId = videoId.ToString(),
                UserId = userId.ToString(),
                PositionTicks = positionTicks
            };

            var response = await client.UpdateWatchProgressAsync(request,
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            return response.Success;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(ex, "Video module is not available for UpdateWatchProgress");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update watch progress via gRPC");
            return false;
        }
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
