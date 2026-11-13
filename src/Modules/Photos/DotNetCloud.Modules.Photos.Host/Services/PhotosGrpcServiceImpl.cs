using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Protos;
using DotNetCloud.Modules.Photos.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.Photos.Host.Services;

/// <summary>
/// gRPC service implementation for the Photos module.
/// Exposes photo and album operations over gRPC for the core server to invoke.
/// </summary>
public sealed class PhotosGrpcServiceImpl : PhotosGrpcService.PhotosGrpcServiceBase
{
    private readonly PhotoService _photoService;
    private readonly AlbumService _albumService;
    private readonly PhotoGeoService _geoService;
    private readonly IPhotoShareService _shareService;
    private readonly IPhotoIndexingCallback _indexingCallback;
    private readonly IModuleUserAssertionAccessor? _assertionAccessor;
    private readonly ILogger<PhotosGrpcServiceImpl> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosGrpcServiceImpl"/> class.
    /// </summary>
    /// <param name="photoService">Photo CRUD service.</param>
    /// <param name="albumService">Album service.</param>
    /// <param name="geoService">Geo query service.</param>
    /// <param name="shareService">Photo and album sharing service.</param>
    /// <param name="indexingCallback">Library indexing callback used by the core's media import.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="assertionAccessor">Resolves the acting user the core asserted, when present.</param>
    public PhotosGrpcServiceImpl(
        PhotoService photoService,
        AlbumService albumService,
        PhotoGeoService geoService,
        IPhotoShareService shareService,
        IPhotoIndexingCallback indexingCallback,
        ILogger<PhotosGrpcServiceImpl> logger,
        IModuleUserAssertionAccessor? assertionAccessor = null)
    {
        _photoService = photoService;
        _albumService = albumService;
        _geoService = geoService;
        _shareService = shareService;
        _indexingCallback = indexingCallback;
        _logger = logger;
        _assertionAccessor = assertionAccessor;
    }

    /// <inheritdoc />
    public override async Task<PhotoResponse> CreatePhoto(CreatePhotoRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreatePhoto called for user {UserId}", request.UserId);
        try
        {
            var caller = ParseCaller(request.UserId);
            var photo = await _photoService.CreatePhotoAsync(
                Guid.Parse(request.FileNodeId),
                request.FileName,
                request.MimeType,
                request.SizeBytes,
                caller.UserId,
                caller,
                context.CancellationToken);
            return new PhotoResponse { Success = true, Photo = MapPhoto(photo) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreatePhoto failed");
            return new PhotoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PhotoResponse> GetPhoto(GetPhotoRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var photoId = Guid.Parse(request.PhotoId);
            var photo = await _photoService.GetPhotoAsync(photoId, caller, context.CancellationToken);
            if (photo is null)
                return new PhotoResponse { Success = false, ErrorMessage = "Photo not found." };
            return new PhotoResponse { Success = true, Photo = MapPhoto(photo) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPhoto failed");
            return new PhotoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListPhotosResponse> ListPhotos(ListPhotosRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var photos = await _photoService.ListPhotosAsync(caller, request.Skip, request.Take, context.CancellationToken);
            var response = new ListPhotosResponse { Success = true };
            foreach (var photo in photos)
                response.Photos.Add(MapPhoto(photo));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListPhotos failed");
            return new ListPhotosResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<DeleteResponse> DeletePhoto(DeletePhotoRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var photoId = Guid.Parse(request.PhotoId);
            await _photoService.DeletePhotoAsync(photoId, caller, context.CancellationToken);
            return new DeleteResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeletePhoto failed");
            return new DeleteResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<PhotoResponse> ToggleFavorite(ToggleFavoriteRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var photoId = Guid.Parse(request.PhotoId);
            var photo = await _photoService.ToggleFavoriteAsync(photoId, caller, context.CancellationToken);
            return new PhotoResponse { Success = true, Photo = MapPhoto(photo) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ToggleFavorite failed");
            return new PhotoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListPhotosResponse> GetTimeline(GetTimelineRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var from = DateTime.Parse(request.FromUtc);
            var to = DateTime.Parse(request.ToUtc);
            var photos = await _photoService.GetTimelineAsync(caller, from, to, context.CancellationToken);
            var response = new ListPhotosResponse { Success = true };
            foreach (var photo in photos)
                response.Photos.Add(MapPhoto(photo));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTimeline failed");
            return new ListPhotosResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<AlbumResponse> CreateAlbum(CreateAlbumRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateAlbum called for user {UserId}", request.UserId);
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new CreateAlbumDto
            {
                Title = request.Title,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description
            };
            var album = await _albumService.CreateAlbumAsync(dto, caller, context.CancellationToken);
            return new AlbumResponse { Success = true, Album = MapAlbum(album) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAlbum failed");
            return new AlbumResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<AlbumResponse> GetAlbum(GetAlbumRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var albumId = Guid.Parse(request.AlbumId);
            var album = await _albumService.GetAlbumAsync(albumId, caller, context.CancellationToken);
            if (album is null)
                return new AlbumResponse { Success = false, ErrorMessage = "Album not found." };
            return new AlbumResponse { Success = true, Album = MapAlbum(album) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAlbum failed");
            return new AlbumResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListAlbumsResponse> ListAlbums(ListAlbumsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var albums = await _albumService.ListAlbumsAsync(caller, context.CancellationToken);
            var response = new ListAlbumsResponse { Success = true };
            foreach (var album in albums)
                response.Albums.Add(MapAlbum(album));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAlbums failed");
            return new ListAlbumsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> AddPhotoToAlbum(AddPhotoToAlbumRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var albumId = Guid.Parse(request.AlbumId);
            var photoId = Guid.Parse(request.PhotoId);
            await _albumService.AddPhotoToAlbumAsync(albumId, photoId, caller, context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddPhotoToAlbum failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> RemovePhotoFromAlbum(RemovePhotoFromAlbumRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var albumId = Guid.Parse(request.AlbumId);
            var photoId = Guid.Parse(request.PhotoId);
            await _albumService.RemovePhotoFromAlbumAsync(albumId, photoId, caller, context.CancellationToken);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RemovePhotoFromAlbum failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GetGeoClustersResponse> GetGeoClusters(GetGeoClustersRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var clusters = await _geoService.GetGeoClustersAsync(Guid.Parse(request.UserId), request.GridSizeDegrees, context.CancellationToken);
            var response = new GetGeoClustersResponse { Success = true };
            foreach (var cluster in clusters)
            {
                response.Clusters.Add(new GeoClusterMessage
                {
                    Latitude = cluster.Latitude,
                    Longitude = cluster.Longitude,
                    PhotoCount = cluster.PhotoCount,
                    RepresentativePhotoId = cluster.RepresentativePhotoId.ToString(),
                    RadiusMetres = cluster.RadiusMetres
                });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGeoClusters failed");
            return new GetGeoClustersResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── Mapping helpers ─────────────────────────────────────────────

    private static CallerContext ParseCaller(string userId)
    {
        return new CallerContext(Guid.Parse(userId), [], CallerType.Module);
    }

    private static PhotoMessage MapPhoto(PhotoDto dto)
    {
        return new PhotoMessage
        {
            Id = dto.Id.ToString(),
            FileNodeId = dto.FileNodeId.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            FileName = dto.FileName,
            MimeType = dto.MimeType,
            SizeBytes = dto.SizeBytes,
            Width = dto.Width ?? 0,
            Height = dto.Height ?? 0,
            IsFavorite = dto.IsFavorite,
            TakenAt = dto.TakenAt.ToString("O"),
            CreatedAt = dto.CreatedAt.ToString("O"),
            HasEdits = dto.HasEdits
        };
    }

    private static AlbumMessage MapAlbum(AlbumDto dto)
    {
        return new AlbumMessage
        {
            Id = dto.Id.ToString(),
            OwnerId = dto.OwnerId.ToString(),
            Title = dto.Title,
            Description = dto.Description ?? "",
            CoverPhotoId = dto.CoverPhotoId?.ToString() ?? "",
            PhotoCount = dto.PhotoCount,
            CreatedAt = dto.CreatedAt.ToString("O"),
            IsShared = dto.IsShared
        };
    }

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return;

        var caller = new CallerContext(userId, ["user"], CallerType.User);

        var photos = await _photoService.ListPhotosAsync(
            caller, skip: 0, take: int.MaxValue, context.CancellationToken);

        foreach (var photo in photos)
        {
            var doc = MapPhotoToSearchableDocument(photo);
            await responseStream.WriteAsync(doc, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<SearchableDocumentResponse> GetSearchableDocument(
        GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId))
            return new SearchableDocumentResponse { Found = false };

        var photo = await _photoService.GetPhotoAsync(
            entityId,
            new CallerContext(Guid.Empty, ["system"], CallerType.System),
            context.CancellationToken);

        if (photo is null)
            return new SearchableDocumentResponse { Found = false };

        return new SearchableDocumentResponse
        {
            Found = true,
            Document = MapPhotoToSearchableDocument(photo)
        };
    }

    /// <summary>
    /// Resolves the acting user of a core-initiated call.
    /// </summary>    /// <remarks>
    /// The user comes from the core-signed assertion carried on the call
    /// (<see cref="ModuleUserAssertionGrpcInterceptor"/> publishes it to the ambient flow), never
    /// from a request field. A call with no verified assertion is refused: these RPCs exist only for
    /// the core acting on a signed-in user's behalf.
    /// </remarks>
    private Guid RequireActingUser()
    {
        var userId = _assertionAccessor?.GetCurrentUserId();
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "This operation requires a verified end-user assertion."));
        }

        return userId.Value;
    }

    /// <inheritdoc />
    public override async Task<IndexPhotoResponse> IndexPhoto(IndexPhotoRequest request, ServerCallContext context)
    {
        try
        {
            var ownerId = RequireActingUser();

            if (!Guid.TryParse(request.FileNodeId, out var fileNodeId))
            {
                return new IndexPhotoResponse { Success = false, ErrorMessage = "A valid file node id is required." };
            }

            await _indexingCallback.IndexPhotoAsync(
                fileNodeId,
                request.FileName,
                request.MimeType,
                request.SizeBytes,
                ownerId,
                context.CancellationToken);

            return new IndexPhotoResponse { Success = true };
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IndexPhoto failed");
            return new IndexPhotoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GetIndexedFileNodeIdsResponse> GetIndexedFileNodeIds(
        GetIndexedFileNodeIdsRequest request, ServerCallContext context)
    {
        var ownerId = RequireActingUser();
        var ids = await _indexingCallback.GetIndexedFileNodeIdsAsync(ownerId, context.CancellationToken);

        var response = new GetIndexedFileNodeIdsResponse();
        response.FileNodeIds.AddRange(ids.Select(id => id.ToString()));
        return response;
    }

    /// <inheritdoc />
    public override async Task<RemoveDeletedPhotosResponse> RemoveDeletedPhotos(
        RemoveDeletedPhotosRequest request, ServerCallContext context)
    {
        var ownerId = RequireActingUser();

        var fileNodeIds = new List<Guid>();
        foreach (var id in request.FileNodeIds)
        {
            if (Guid.TryParse(id, out var parsed))
            {
                fileNodeIds.Add(parsed);
            }
        }

        if (fileNodeIds.Count == 0)
        {
            return new RemoveDeletedPhotosResponse { Removed = 0 };
        }

        var removed = await _indexingCallback.RemoveDeletedPhotosAsync(fileNodeIds, ownerId, context.CancellationToken);
        return new RemoveDeletedPhotosResponse { Removed = removed };
    }

    /// <inheritdoc />
    public override async Task<GetSharedWithMeResponse> GetSharedWithMe(
        GetSharedWithMeRequest request, ServerCallContext context)
    {
        var userId = RequireActingUser();
        var caller = new CallerContext(userId, [], CallerType.User);

        var shares = await _shareService.GetSharedWithMeAsync(caller, context.CancellationToken);
        var response = new GetSharedWithMeResponse();

        foreach (var share in shares)
        {
            // The core cannot resolve a title (it has no access to this schema), so the module
            // answers with one — an album title or a file name — or leaves it blank when the
            // entity is gone and the entry is stale.
            var title = string.Empty;

            if (share.AlbumId is { } albumId)
            {
                var album = await _albumService.GetAlbumAsync(albumId, caller, context.CancellationToken);
                title = album?.Title ?? string.Empty;
            }
            else if (share.PhotoId is { } photoId)
            {
                var photo = await _photoService.GetPhotoAsync(photoId, caller, context.CancellationToken);
                title = photo?.FileName ?? string.Empty;
            }

            response.Shares.Add(new PhotoSharedItemMessage
            {
                ShareId = share.Id.ToString(),
                PhotoId = share.PhotoId?.ToString() ?? string.Empty,
                AlbumId = share.AlbumId?.ToString() ?? string.Empty,
                Title = title,
                Permission = share.Permission.ToString(),
                CreatedAt = share.CreatedAt.ToString("O"),
            });
        }

        return response;
    }

    private static SearchableDocument MapPhotoToSearchableDocument(PhotoDto photo)
    {
        var contentParts = new List<string> { photo.FileName };
        if (photo.Metadata is not null)
        {
            if (!string.IsNullOrEmpty(photo.Metadata.CameraModel))
                contentParts.Add(photo.Metadata.CameraModel);
        }

        var doc = new SearchableDocument
        {
            ModuleId = "photos",
            EntityId = photo.Id.ToString(),
            EntityType = "Photo",
            Title = photo.FileName,
            Content = string.Join(" ", contentParts),
            Summary = $"{photo.MimeType} ({photo.Width}x{photo.Height})",
            OwnerId = photo.OwnerId.ToString(),
            CreatedAt = photo.CreatedAt.ToString("O"),
            UpdatedAt = photo.UpdatedAt.ToString("O")
        };

        doc.Metadata["MimeType"] = photo.MimeType;
        doc.Metadata["TakenAt"] = photo.TakenAt.ToString("O");
        if (photo.Metadata?.CameraModel is not null)
            doc.Metadata["Camera"] = photo.Metadata.CameraModel;

        return doc;
    }
}
