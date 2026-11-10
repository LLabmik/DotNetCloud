using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Grpc.Clients;
using DotNetCloud.Modules.Photos.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services.Media;

/// <summary>
/// The core half of the Photos library indexing bridge.
/// </summary>
/// <remarks>
/// A sandboxed module cannot subscribe to the Files module's <c>FileUploadedEvent</c> — it never sees
/// another module's events — so the core's media import tells the Photos module which nodes are part
/// of a user's library. Every call carries a core-signed assertion naming that user; the module
/// resolves the owner from the assertion and never from a request field.
/// </remarks>
public interface IPhotoIndexingClient
{
    /// <summary>Records an image node as a photo in the user's library (idempotent).</summary>
    /// <param name="fileNodeId">The Files node.</param>
    /// <param name="fileName">Display file name.</param>
    /// <param name="mimeType">MIME type.</param>
    /// <param name="sizeBytes">Size in bytes.</param>
    /// <param name="ownerId">The library owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the module acknowledged the index request.</returns>
    Task<bool> IndexPhotoAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the file node ids the module already has for the user.</summary>
    /// <param name="ownerId">The library owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexed node ids; empty when the module is unreachable.</returns>
    Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes photos whose backing nodes no longer exist.</summary>
    /// <param name="deletedFileNodeIds">The nodes that were deleted.</param>
    /// <param name="ownerId">The library owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of photo records removed.</returns>
    Task<int> RemoveDeletedPhotosAsync(
        IReadOnlyCollection<Guid> deletedFileNodeIds,
        Guid ownerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// gRPC implementation of <see cref="IPhotoIndexingClient"/>.
/// </summary>
internal sealed class PhotosIndexingGrpcClient : IPhotoIndexingClient
{
    private const string PhotosModuleId = "dotnetcloud.photos";

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    private readonly Supervisor.ModuleAssertionKeyRing _assertions;
    private readonly Supervisor.IModuleGrpcChannelProvider _channelManager;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<PhotosIndexingGrpcClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;

    /// <summary>Initializes a new instance of the <see cref="PhotosIndexingGrpcClient"/> class.</summary>
    /// <param name="assertions">Mints the user assertion each call carries.</param>
    /// <param name="channelManager">Provides the peer-authenticated channel.</param>
    /// <param name="endpointProvider">Resolves the module's endpoint.</param>
    /// <param name="logger">Logger.</param>
    public PhotosIndexingGrpcClient(
        Supervisor.ModuleAssertionKeyRing assertions,
        Supervisor.IModuleGrpcChannelProvider channelManager,
        ModuleEndpointProvider endpointProvider,
        ILogger<PhotosIndexingGrpcClient> logger)
    {
        _assertions = assertions;
        _channelManager = channelManager;
        _endpointProvider = endpointProvider;
        _logger = logger;

        // The channel belongs to GrpcChannelManager and is shared: never dispose it here.
        _channel = new Lazy<GrpcChannel>(() =>
            _channelManager.GetOrCreateChannel(
                PhotosModuleId,
                _endpointProvider.GetEndpoint(PhotosModuleId)));
    }

    /// <inheritdoc />
    public async Task<bool> IndexPhotoAsync(
        Guid fileNodeId,
        string fileName,
        string mimeType,
        long sizeBytes,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new PhotosGrpcService.PhotosGrpcServiceClient(_channel.Value);
            var response = await client.IndexPhotoAsync(
                new IndexPhotoRequest
                {
                    FileNodeId = fileNodeId.ToString(),
                    FileName = fileName,
                    MimeType = mimeType,
                    SizeBytes = sizeBytes,
                },
                AssertionMetadata(ownerId, "photos.index"),
                deadline: DateTime.UtcNow.Add(CallTimeout),
                cancellationToken: cancellationToken);

            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "The Photos module refused an index request ({Status})", ex.StatusCode);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetIndexedFileNodeIdsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new PhotosGrpcService.PhotosGrpcServiceClient(_channel.Value);
            var response = await client.GetIndexedFileNodeIdsAsync(
                new GetIndexedFileNodeIdsRequest(),
                AssertionMetadata(ownerId, "photos.index"),
                deadline: DateTime.UtcNow.Add(CallTimeout),
                cancellationToken: cancellationToken);

            var ids = new HashSet<Guid>();
            foreach (var id in response.FileNodeIds)
            {
                if (Guid.TryParse(id, out var parsed))
                {
                    ids.Add(parsed);
                }
            }

            return ids;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "The Photos module could not report indexed nodes ({Status})", ex.StatusCode);
            return new HashSet<Guid>();
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveDeletedPhotosAsync(
        IReadOnlyCollection<Guid> deletedFileNodeIds,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deletedFileNodeIds);

        if (deletedFileNodeIds.Count == 0)
        {
            return 0;
        }

        try
        {
            var request = new RemoveDeletedPhotosRequest();
            request.FileNodeIds.AddRange(deletedFileNodeIds.Select(id => id.ToString()));

            var client = new PhotosGrpcService.PhotosGrpcServiceClient(_channel.Value);
            var response = await client.RemoveDeletedPhotosAsync(
                request,
                AssertionMetadata(ownerId, "photos.index"),
                deadline: DateTime.UtcNow.Add(CallTimeout),
                cancellationToken: cancellationToken);

            return response.Removed;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "The Photos module could not remove deleted nodes ({Status})", ex.StatusCode);
            return 0;
        }
    }

    private Metadata AssertionMetadata(Guid ownerId, string operation) =>
        ModuleIdentityMetadata.Create(
            "dotnetcloud.core",
            _assertions.Mint(ownerId, PhotosModuleId, operation));
}
