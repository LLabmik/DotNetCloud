using DotNetCloud.Core.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes the <see cref="AdminSharedFolderCleanupService"/> to the event bus on startup.
/// </summary>
internal sealed class AdminSharedFolderCleanupSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly AdminSharedFolderCleanupService _cleanupService;
    private readonly ILogger<AdminSharedFolderCleanupSubscriber> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderCleanupSubscriber"/> class.
    /// </summary>
    public AdminSharedFolderCleanupSubscriber(
        IEventBus eventBus,
        AdminSharedFolderCleanupService cleanupService,
        ILogger<AdminSharedFolderCleanupSubscriber> logger)
    {
        _eventBus = eventBus;
        _cleanupService = cleanupService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<AdminSharedFolderDeletedEvent>(_cleanupService, cancellationToken);
        _logger.LogInformation("Admin shared folder cleanup subscriber started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _eventBus.UnsubscribeAsync<AdminSharedFolderDeletedEvent>(_cleanupService, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing admin shared folder cleanup handler");
        }

        _logger.LogInformation("Admin shared folder cleanup subscriber stopped");
    }
}
