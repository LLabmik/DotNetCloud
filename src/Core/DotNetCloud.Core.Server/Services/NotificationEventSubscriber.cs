using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes cross-module notification event handlers on startup
/// and unsubscribes them on shutdown.
/// </summary>
internal sealed class NotificationEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IPushNotificationService _pushService;
    private readonly ILoggerFactory _loggerFactory;
    private FileSharedNotificationHandler? _fileSharedHandler;
    private QuotaNotificationHandler? _quotaHandler;
    private PublicLinkAccessedNotificationHandler? _publicLinkHandler;
    private ShareExpiringNotificationHandler? _shareExpiringHandler;

    public NotificationEventSubscriber(
        IEventBus eventBus,
        IPushNotificationService pushService,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _pushService = pushService;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _fileSharedHandler = new FileSharedNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<FileSharedNotificationHandler>());

        _quotaHandler = new QuotaNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<QuotaNotificationHandler>());

        _publicLinkHandler = new PublicLinkAccessedNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<PublicLinkAccessedNotificationHandler>());

        _shareExpiringHandler = new ShareExpiringNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<ShareExpiringNotificationHandler>());

        await _eventBus.SubscribeAsync<FileSharedEvent>(_fileSharedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaWarningEvent>(_quotaHandler, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaCriticalEvent>(_quotaHandler, cancellationToken);
        await _eventBus.SubscribeAsync<PublicLinkAccessedEvent>(_publicLinkHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ShareExpiringEvent>(_shareExpiringHandler, cancellationToken);

        _loggerFactory.CreateLogger<NotificationEventSubscriber>()
            .LogInformation("Notification event handlers subscribed (FileShared, QuotaWarning, QuotaCritical, PublicLinkAccessed, ShareExpiring)");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_fileSharedHandler is not null)
            await _eventBus.UnsubscribeAsync<FileSharedEvent>(_fileSharedHandler, cancellationToken);

        if (_quotaHandler is not null)
        {
            await _eventBus.UnsubscribeAsync<QuotaWarningEvent>(_quotaHandler, cancellationToken);
            await _eventBus.UnsubscribeAsync<QuotaCriticalEvent>(_quotaHandler, cancellationToken);
        }

        if (_publicLinkHandler is not null)
            await _eventBus.UnsubscribeAsync<PublicLinkAccessedEvent>(_publicLinkHandler, cancellationToken);

        if (_shareExpiringHandler is not null)
            await _eventBus.UnsubscribeAsync<ShareExpiringEvent>(_shareExpiringHandler, cancellationToken);
    }
}
