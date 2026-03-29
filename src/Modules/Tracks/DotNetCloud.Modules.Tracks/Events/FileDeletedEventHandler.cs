using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Handles <see cref="FileDeletedEvent"/> from the Files module to clean up
/// orphaned card attachment references in Tracks boards.
/// </summary>
internal sealed class FileDeletedEventHandler : IEventHandler<FileDeletedEvent>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileDeletedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDeletedEventHandler"/> class.
    /// </summary>
    public FileDeletedEventHandler(IServiceProvider serviceProvider, ILogger<FileDeletedEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileDeletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling FileDeletedEvent: FileNodeId={FileNodeId}, IsPermanent={IsPermanent}",
            @event.FileNodeId, @event.IsPermanent);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<ICardAttachmentCleanupService>();
            await cleanupService.ClearFileReferencesAsync(@event.FileNodeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up card attachments for deleted file {FileNodeId}", @event.FileNodeId);
        }
    }
}
