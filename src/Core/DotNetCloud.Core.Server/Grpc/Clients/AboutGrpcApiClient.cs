using DotNetCloud.Modules.About.Host.Protos;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the About gRPC client used by the Core Server.
/// </summary>
public sealed class AboutGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "AboutGrpc";

    /// <summary>The gRPC address of the About module.</summary>
    public string AboutModuleAddress { get; set; } = "http://localhost:5014";

    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IAboutApiClient"/>.
/// Calls the About module's gRPC service for system information.
/// </summary>
public sealed class AboutGrpcApiClient : IAboutApiClient, IDisposable
{
    private readonly AboutGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<AboutGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<AboutService.AboutServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AboutGrpcApiClient"/> class.</summary>
    public AboutGrpcApiClient(
        IOptions<AboutGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<AboutGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<AboutService.AboutServiceClient>(
            () => new AboutService.AboutServiceClient(_channel.Value));
    }

    /// <inheritdoc />
    public async Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _client.Value.GetAboutInfoAsync(
                new GetAboutInfoRequest { UserId = Guid.Empty.ToString() },
                DeadlineHeaders(ct)).ResponseAsync;

            return resp.Success
                ? new AboutInfoDto
                {
                    Version = resp.Version,
                    Environment = resp.Environment,
                    RuntimeVersion = resp.RuntimeVersion,
                    OsDescription = resp.OsDescription,
                    LicenseStatus = resp.LicenseStatus,
                    Uptime = resp.Uptime
                }
                : null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "About gRPC service unavailable (GetAboutInfo)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "About gRPC error (GetAboutInfo)");
            return null;
        }
    }

    private CallOptions DeadlineHeaders(CancellationToken ct)
        => new(deadline: DateTime.UtcNow.Add(_options.Timeout), cancellationToken: ct);

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.about");
        _logger.LogInformation("AboutGrpcApiClient connecting to {Address}", address);
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
