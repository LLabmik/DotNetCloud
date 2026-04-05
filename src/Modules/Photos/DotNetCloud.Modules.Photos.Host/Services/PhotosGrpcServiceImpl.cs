using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Protos;
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
    private readonly ILogger<PhotosGrpcServiceImpl> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosGrpcServiceImpl"/> class.
    /// </summary>
    public PhotosGrpcServiceImpl(
        PhotoService photoService,
        AlbumService albumService,
        PhotoGeoService geoService,
        ILogger<PhotosGrpcServiceImpl> logger)
    {
        _photoService = photoService;
        _albumService = albumService;
        _geoService = geoService;
        _logger = logger;
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
}
