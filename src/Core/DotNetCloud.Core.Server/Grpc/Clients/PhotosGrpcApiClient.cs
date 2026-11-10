using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Photos.Host.Protos;
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
/// <remarks>
/// Calls carry the supervisor-issued module credential (attached by the managed channel) plus a
/// core-signed user assertion naming the user the call is made for. The Photos host resolves the
/// acting user from that assertion and never from a request field, so the core cannot ask it to act
/// as somebody it did not sign for.
/// </remarks>
public sealed class PhotosGrpcApiClient : IPhotosApiClient, IDisposable
{
    private const string PhotosModuleId = "dotnetcloud.photos";

    private readonly PhotosGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly Supervisor.IModuleGrpcChannelProvider _channelManager;
    private readonly Supervisor.ModuleAssertionKeyRing _assertions;
    private readonly ILogger<PhotosGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="PhotosGrpcApiClient"/> class.</summary>
    /// <param name="options">Client options (timeout).</param>
    /// <param name="endpointProvider">Resolves the module's endpoint.</param>
    /// <param name="channelManager">Provides the peer-authenticated channel.</param>
    /// <param name="assertions">Mints the per-user assertion each user-scoped call carries.</param>
    /// <param name="logger">Logger.</param>
    public PhotosGrpcApiClient(
        IOptions<PhotosGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        Supervisor.IModuleGrpcChannelProvider channelManager,
        Supervisor.ModuleAssertionKeyRing assertions,
        ILogger<PhotosGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _channelManager = channelManager;
        _assertions = assertions;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint(PhotosModuleId);
        _logger.LogInformation("PhotosGrpcApiClient connecting to {Address}", address);

        // Managed channel: it attaches the supervisor-issued credential the module host requires.
        return _channelManager.GetOrCreateChannel(PhotosModuleId, address);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PhotoSharedItem>> GetSharedWithMeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new PhotosGrpcService.PhotosGrpcServiceClient(_channel.Value);

            var response = await client.GetSharedWithMeAsync(
                new GetSharedWithMeRequest(),
                ModuleIdentityMetadata.Create(
                    "dotnetcloud.core",
                    _assertions.Mint(userId, PhotosModuleId, "photos.shared")),
                deadline: DateTime.UtcNow.Add(_options.Timeout),
                cancellationToken: cancellationToken);

            var items = new List<PhotoSharedItem>(response.Shares.Count);

            foreach (var share in response.Shares)
            {
                if (!Guid.TryParse(share.ShareId, out var shareId))
                {
                    continue;
                }

                items.Add(new PhotoSharedItem(
                    shareId,
                    Guid.TryParse(share.PhotoId, out var photoId) ? photoId : null,
                    Guid.TryParse(share.AlbumId, out var albumId) ? albumId : null,
                    share.Title,
                    share.Permission,
                    DateTime.TryParse(share.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var createdAt)
                        ? createdAt
                        : DateTime.UtcNow));
            }

            return items;
        }
        catch (RpcException ex)
        {
            // A module that is stopped or restarting must not break the Files listing; the folder
            // simply reports nothing until it is healthy again.
            _logger.LogWarning(ex, "The Photos module could not list shared items ({Status})", ex.StatusCode);
            return [];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The channel belongs to GrpcChannelManager and is shared, so there is nothing to release.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
