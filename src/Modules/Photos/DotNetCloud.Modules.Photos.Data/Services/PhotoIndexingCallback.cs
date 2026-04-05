using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Implements the photo indexing callback — bridges the Module → Data layer gap.
/// Called by FileUploadedPhotoHandler when an image file is uploaded.
/// </summary>
public sealed class PhotoIndexingCallback : IPhotoIndexingCallback
{
    private readonly PhotoService _photoService;
    private readonly ILogger<PhotoIndexingCallback> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoIndexingCallback"/> class.
    /// </summary>
    public PhotoIndexingCallback(PhotoService photoService, ILogger<PhotoIndexingCallback> logger)
    {
        _photoService = photoService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexPhotoAsync(Guid fileNodeId, string fileName, string mimeType, long sizeBytes, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var caller = new CallerContext(ownerId, ["user"], CallerType.System);
        await _photoService.CreatePhotoAsync(fileNodeId, fileName, mimeType, sizeBytes, ownerId, caller, cancellationToken);

        _logger.LogDebug("Photo indexed for FileNode {FileNodeId} by user {OwnerId}", fileNodeId, ownerId);
    }
}
