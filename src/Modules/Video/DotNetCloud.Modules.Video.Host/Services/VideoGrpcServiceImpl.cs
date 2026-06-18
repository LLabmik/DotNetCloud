using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Host.Protos;
using DotNetCloud.Modules.Video.Services;
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
    private readonly VideoStreamingService _streamingService;
    private readonly IVideoTranscodingService _transcodingService;
    private readonly ILogger<VideoGrpcServiceImpl> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoGrpcServiceImpl"/> class.
    /// </summary>
    public VideoGrpcServiceImpl(
        VideoService videoService,
        VideoCollectionService collectionService,
        VideoStreamingService streamingService,
        IVideoTranscodingService transcodingService,
        ILogger<VideoGrpcServiceImpl> logger)
    {
        _videoService = videoService;
        _collectionService = collectionService;
        _streamingService = streamingService;
        _transcodingService = transcodingService;
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
            var searchResult = await _videoService.SearchAsync(caller, request.Query, request.Take);
            var response = new ListVideosResponse { Success = true };
            foreach (var v in searchResult.StandaloneVideos)
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

    // ── Transcoding RPCs ─────────────────────────────────────────────

    /// <inheritdoc />
    public override async Task<StreamInfoResponse> GetStreamInfo(GetStreamInfoRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.VideoId, out var videoId) || !Guid.TryParse(request.UserId, out var userId))
                return new StreamInfoResponse { Success = false, ErrorMessage = "Invalid GUID format." };

            var caller = ParseCaller(request.UserId);
            var video = await _videoService.GetVideoAsync(videoId, caller);
            if (video is null)
                return new StreamInfoResponse { Success = false, ErrorMessage = "Video not found." };

            var token = _streamingService.GenerateStreamToken(videoId, userId);
            var canDirectPlay = await _transcodingService.CanDirectPlayAsync(
                video.FileName, video.MimeType, context.CancellationToken);

            return new StreamInfoResponse
            {
                Success = true,
                CanDirectPlay = canDirectPlay,
                StreamUrl = $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}" +
                            (canDirectPlay ? "" : "&forceTranscode=true"),
                MimeType = canDirectPlay ? video.MimeType : "video/mp4"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStreamInfo failed");
            return new StreamInfoResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<RequestTranscodeResponse> RequestTranscode(RequestTranscodeRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.VideoId, out var videoId) || !Guid.TryParse(request.UserId, out var userId))
                return new RequestTranscodeResponse { Success = false, ErrorMessage = "Invalid GUID format." };

            var caller = ParseCaller(request.UserId);
            var token = _streamingService.GenerateStreamToken(videoId, userId);
            var video = await _videoService.GetVideoAsync(videoId, caller);
            if (video is null)
                return new RequestTranscodeResponse { Success = false, ErrorMessage = "Video not found." };

            var (jobId, _) = await _transcodingService.TranscodeAsync(
                videoId, userId, video.FileName, video.MimeType,
                ct: context.CancellationToken);

            return new RequestTranscodeResponse
            {
                Success = true,
                JobId = jobId,
                StreamUrl = $"/api/v1/videos/{videoId}/stream?token={Uri.EscapeDataString(token)}&forceTranscode=true"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestTranscode failed");
            return new RequestTranscodeResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override Task<TranscodeProgressResponse> GetTranscodeProgress(GetTranscodeProgressRequest request, ServerCallContext context)
    {
        try
        {
            var job = _transcodingService.GetProgress(request.JobId);
            if (job is null)
                return Task.FromResult(new TranscodeProgressResponse { Success = false, ErrorMessage = "Job not found." });

            return Task.FromResult(new TranscodeProgressResponse
            {
                Success = true,
                JobId = job.Id,
                Status = job.Status.ToString(),
                ProgressPercent = job.ProgressPercent,
                CurrentTime = job.CurrentTime.ToString(@"hh\:mm\:ss"),
                Speed = job.Speed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTranscodeProgress failed");
            return Task.FromResult(new TranscodeProgressResponse { Success = false, ErrorMessage = ex.Message });
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

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        // Index ALL videos across all users — each tagged with correct OwnerId
        var videos = await _videoService.ListAllVideosAsync(
            skip: 0, take: int.MaxValue, cancellationToken: context.CancellationToken);

        foreach (var video in videos)
        {
            var doc = MapVideoToSearchableDocument(video);
            await responseStream.WriteAsync(doc, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<SearchableDocumentResponse> GetSearchableDocument(
        GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId))
            return new SearchableDocumentResponse { Found = false };

        var video = await _videoService.GetVideoAsync(
            entityId,
            new CallerContext(Guid.Empty, ["system"], CallerType.System),
            context.CancellationToken);

        if (video is null)
            return new SearchableDocumentResponse { Found = false };

        return new SearchableDocumentResponse
        {
            Found = true,
            Document = MapVideoToSearchableDocument(video)
        };
    }

    private static SearchableDocument MapVideoToSearchableDocument(VideoDto video)
    {
        var doc = new SearchableDocument
        {
            ModuleId = "video",
            EntityId = video.Id.ToString(),
            EntityType = "Video",
            Title = video.Title,
            Content = string.Empty,
            Summary = video.FileName,
            OwnerId = video.OwnerId.ToString(),
            CreatedAt = video.CreatedAt.ToString("O"),
            UpdatedAt = video.CreatedAt.ToString("O")
        };

        doc.Metadata["MimeType"] = video.MimeType;
        doc.Metadata["Duration"] = video.Duration.ToString();
        doc.Metadata["FileName"] = video.FileName;

        return doc;
    }
}
