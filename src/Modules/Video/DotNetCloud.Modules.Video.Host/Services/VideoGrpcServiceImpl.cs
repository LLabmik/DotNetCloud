using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// gRPC service implementation for the Video module.
/// Exposes video operations over gRPC for the core server to invoke.
/// </summary>
public sealed class VideoGrpcServiceImpl : VideoGrpcService.VideoGrpcServiceBase
{
    private readonly VideoService _videoService;
    private readonly VideoCollectionService _collectionService;
    private readonly WatchProgressService _watchProgressService;
    private readonly VideoStreamingService _streamingService;
    private readonly ILogger<VideoGrpcServiceImpl> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoGrpcServiceImpl"/> class.
    /// </summary>
    public VideoGrpcServiceImpl(
        VideoService videoService,
        VideoCollectionService collectionService,
        WatchProgressService watchProgressService,
        VideoStreamingService streamingService,
        ILogger<VideoGrpcServiceImpl> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _watchProgressService = watchProgressService;
        _streamingService = streamingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<VideoResponse> GetVideo(GetVideoRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var video = await _videoService.GetVideoAsync(Guid.Parse(request.VideoId), caller);
            if (video is null)
                return new VideoResponse { Success = false, ErrorMessage = "Video not found." };
            return new VideoResponse { Success = true, Video = MapVideo(video) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetVideo failed");
            return new VideoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListVideosResponse> ListVideos(ListVideosRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var videos = await _videoService.ListVideosAsync(caller, request.Skip, request.Take);
            var response = new ListVideosResponse { Success = true };
            foreach (var v in videos)
                response.Videos.Add(MapVideo(v));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListVideos failed");
            return new ListVideosResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListVideosResponse> SearchVideos(SearchVideosRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var videos = await _videoService.SearchAsync(caller, request.Query, request.Take);
            var response = new ListVideosResponse { Success = true };
            foreach (var v in videos)
                response.Videos.Add(MapVideo(v));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchVideos failed");
            return new ListVideosResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CollectionResponse> GetCollection(GetCollectionRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var collection = await _collectionService.GetCollectionAsync(Guid.Parse(request.CollectionId), caller);
            if (collection is null)
                return new CollectionResponse { Success = false, ErrorMessage = "Collection not found." };
            return new CollectionResponse { Success = true, Collection = MapCollection(collection) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCollection failed");
            return new CollectionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ListCollectionsResponse> ListCollections(ListCollectionsRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var collections = await _collectionService.ListCollectionsAsync(caller);
            var response = new ListCollectionsResponse { Success = true };
            foreach (var c in collections)
                response.Collections.Add(MapCollection(c));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListCollections failed");
            return new ListCollectionsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<CollectionResponse> CreateCollection(CreateCollectionRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new CreateVideoCollectionDto
            {
                Name = request.Name,
                Description = string.IsNullOrEmpty(request.Description) ? null : request.Description
            };
            var collection = await _collectionService.CreateCollectionAsync(dto, caller);
            return new CollectionResponse { Success = true, Collection = MapCollection(collection) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCollection failed");
            return new CollectionResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> DeleteCollection(DeleteCollectionRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _collectionService.DeleteCollectionAsync(Guid.Parse(request.CollectionId), caller);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteCollection failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<WatchProgressResponse> GetWatchProgress(GetWatchProgressRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var progress = await _watchProgressService.GetProgressAsync(Guid.Parse(request.VideoId), caller);
            if (progress is null)
                return new WatchProgressResponse { Success = false, ErrorMessage = "No progress found." };
            return new WatchProgressResponse { Success = true, Progress = MapWatchProgress(progress) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetWatchProgress failed");
            return new WatchProgressResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<WatchProgressResponse> UpdateWatchProgress(UpdateWatchProgressRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            var dto = new UpdateWatchProgressDto { PositionTicks = request.PositionTicks };
            await _watchProgressService.UpdateProgressAsync(Guid.Parse(request.VideoId), dto, caller);
            // Re-fetch the progress to return
            var progress = await _watchProgressService.GetProgressAsync(Guid.Parse(request.VideoId), caller);
            if (progress is null)
                return new WatchProgressResponse { Success = false, ErrorMessage = "Progress not found after update." };
            return new WatchProgressResponse { Success = true, Progress = MapWatchProgress(progress) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateWatchProgress failed");
            return new WatchProgressResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GenericResponse> RecordView(RecordViewRequest request, ServerCallContext context)
    {
        try
        {
            var caller = ParseCaller(request.UserId);
            await _watchProgressService.RecordViewAsync(Guid.Parse(request.VideoId), caller, request.DurationSeconds);
            return new GenericResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordView failed");
            return new GenericResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<StreamTokenResponse> GenerateStreamToken(GenerateStreamTokenRequest request, ServerCallContext context)
    {
        try
        {
            var token = _streamingService.GenerateStreamToken(Guid.Parse(request.VideoId), Guid.Parse(request.UserId));
            return Task.FromResult(new StreamTokenResponse
            {
                Success = true,
                Token = token,
                ExpiresInMinutes = (int)_streamingService.StreamTokenLifetime.TotalMinutes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateStreamToken failed");
            return Task.FromResult(new StreamTokenResponse { Success = false, ErrorMessage = ex.Message });
        }
    }

    // ── Mapping helpers ─────────────────────────────────────────────

    private static CallerContext ParseCaller(string userId)
    {
        return new CallerContext(Guid.Parse(userId), [], CallerType.Module);
    }

    private static VideoMessage MapVideo(VideoDto dto)
    {
        return new VideoMessage
        {
            Id = dto.Id.ToString(),
            FileNodeId = dto.FileNodeId.ToString(),
            Title = dto.Title,
            FileName = dto.FileName,
            MimeType = dto.MimeType ?? "",
            SizeBytes = dto.SizeBytes,
            DurationTicks = dto.Duration.Ticks,
            IsFavorite = dto.IsFavorite,
            ViewCount = dto.ViewCount,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static CollectionMessage MapCollection(VideoCollectionDto dto)
    {
        return new CollectionMessage
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? "",
            VideoCount = dto.VideoCount,
            CreatedAt = dto.CreatedAt.ToString("O")
        };
    }

    private static WatchProgressMessage MapWatchProgress(WatchProgressDto dto)
    {
        return new WatchProgressMessage
        {
            VideoId = dto.VideoId.ToString(),
            PositionTicks = dto.PositionTicks,
            IsCompleted = dto.ProgressPercent >= 90,
            UpdatedAt = dto.LastWatchedAt.ToString("O")
        };
    }
}
