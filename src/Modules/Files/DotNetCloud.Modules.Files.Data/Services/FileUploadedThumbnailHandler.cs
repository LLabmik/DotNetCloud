using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Handles <see cref="FileUploadedEvent"/> by generating thumbnails for newly uploaded files.
/// Reconstructs file content from chunks via <see cref="IDownloadService"/> and passes
/// the stream to <see cref="IThumbnailService"/> for thumbnail generation.
/// Uses <see cref="IServiceScopeFactory"/> to create scoped DI contexts for database access.
/// </summary>
internal sealed class FileUploadedThumbnailHandler : IEventHandler<FileUploadedEvent>
{
    private static readonly HashSet<string> SupportedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif",
        "image/webp", "image/bmp", "image/tiff"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<FileUploadedThumbnailHandler> _logger;

    public FileUploadedThumbnailHandler(
        IServiceScopeFactory scopeFactory,
        IThumbnailService thumbnailService,
        ILogger<FileUploadedThumbnailHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(@event.MimeType) || !SupportedImageMimeTypes.Contains(@event.MimeType))
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();

            var caller = new CallerContext(@event.UploadedByUserId, [], CallerType.System);
            await using var contentStream = await downloadService.DownloadCurrentAsync(
                @event.FileNodeId, caller, cancellationToken);

            await _thumbnailService.GenerateThumbnailFromStreamAsync(
                @event.FileNodeId, contentStream, @event.MimeType, cancellationToken);

            _logger.LogDebug("Thumbnail generated for uploaded file {FileNodeId} ({FileName})",
                @event.FileNodeId, @event.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for uploaded file {FileNodeId} ({FileName})",
                @event.FileNodeId, @event.FileName);
        }
    }
}
