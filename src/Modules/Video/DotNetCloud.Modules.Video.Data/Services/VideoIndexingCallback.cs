using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Implements the video indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedVideoHandler when a video file is uploaded.
/// </summary>
public sealed class VideoIndexingCallback : IVideoIndexingCallback
{
    private readonly VideoService _videoService;
    private readonly ILogger<VideoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoIndexingCallback"/> class.
    /// </summary>
    public VideoIndexingCallback(VideoService videoService, ILogger<VideoIndexingCallback> logger)
    {
        _videoService = videoService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexVideoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        await _videoService.CreateVideoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        _logger.LogDebug("Video indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }
}
