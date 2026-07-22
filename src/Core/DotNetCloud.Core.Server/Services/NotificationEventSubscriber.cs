using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.PushNotifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Subscribes cross-module notification event handlers on startup
/// and unsubscribes them on shutdown.
/// </summary>
internal sealed class NotificationEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IPushNotificationService _pushService;
    private readonly IRealtimeBroadcaster _broadcaster;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private FileSharedNotificationHandler? _fileSharedHandler;
    private QuotaNotificationHandler? _quotaHandler;
    private PublicLinkAccessedNotificationHandler? _publicLinkHandler;
    private ShareExpiringNotificationHandler? _shareExpiringHandler;
    private ResourceSharedNotificationHandler? _resourceSharedHandler;
    private UserMentionedNotificationHandler? _userMentionedHandler;
    private ReminderNotificationHandler? _reminderHandler;
    private InAppNotificationEventHandler? _inAppNotificationHandler;
    private CalendarEventDeletedRealtimeHandler? _calDeletedHandler;
    private CalendarEventUpdatedRealtimeHandler? _calUpdatedHandler;

    public NotificationEventSubscriber(
        IEventBus eventBus,
        IPushNotificationService pushService,
        IRealtimeBroadcaster broadcaster,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _pushService = pushService;
        _broadcaster = broadcaster;
        _scopeFactory = scopeFactory;
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

        _resourceSharedHandler = new ResourceSharedNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<ResourceSharedNotificationHandler>());

        _userMentionedHandler = new UserMentionedNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<UserMentionedNotificationHandler>());

        _reminderHandler = new ReminderNotificationHandler(
            _pushService,
            _loggerFactory.CreateLogger<ReminderNotificationHandler>());

        _inAppNotificationHandler = new InAppNotificationEventHandler(_scopeFactory);

        _calDeletedHandler = new CalendarEventDeletedRealtimeHandler(
            _broadcaster,
            _loggerFactory.CreateLogger<CalendarEventDeletedRealtimeHandler>());

        _calUpdatedHandler = new CalendarEventUpdatedRealtimeHandler(
            _broadcaster,
            _loggerFactory.CreateLogger<CalendarEventUpdatedRealtimeHandler>());

        await _eventBus.SubscribeAsync<FileSharedEvent>(_fileSharedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaWarningEvent>(_quotaHandler, cancellationToken);
        await _eventBus.SubscribeAsync<QuotaCriticalEvent>(_quotaHandler, cancellationToken);
        await _eventBus.SubscribeAsync<PublicLinkAccessedEvent>(_publicLinkHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ShareExpiringEvent>(_shareExpiringHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ResourceSharedEvent>(_resourceSharedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<UserMentionedEvent>(_userMentionedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ReminderTriggeredEvent>(_reminderHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ResourceSharedEvent>(_inAppNotificationHandler, cancellationToken);
        await _eventBus.SubscribeAsync<UserMentionedEvent>(_inAppNotificationHandler, cancellationToken);
        await _eventBus.SubscribeAsync<ReminderTriggeredEvent>(_inAppNotificationHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventDeletedEvent>(_calDeletedHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CalendarEventUpdatedEvent>(_calUpdatedHandler, cancellationToken);

        _loggerFactory.CreateLogger<NotificationEventSubscriber>()
            .LogInformation("Notification event handlers subscribed (FileShared, QuotaWarning, QuotaCritical, PublicLinkAccessed, ShareExpiring, ResourceShared, UserMentioned, Reminder)");
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

        if (_resourceSharedHandler is not null)
            await _eventBus.UnsubscribeAsync<ResourceSharedEvent>(_resourceSharedHandler, cancellationToken);

        if (_userMentionedHandler is not null)
            await _eventBus.UnsubscribeAsync<UserMentionedEvent>(_userMentionedHandler, cancellationToken);

        if (_reminderHandler is not null)
            await _eventBus.UnsubscribeAsync<ReminderTriggeredEvent>(_reminderHandler, cancellationToken);

        if (_inAppNotificationHandler is not null)
        {
            await _eventBus.UnsubscribeAsync<ResourceSharedEvent>(_inAppNotificationHandler, cancellationToken);
            await _eventBus.UnsubscribeAsync<UserMentionedEvent>(_inAppNotificationHandler, cancellationToken);
            await _eventBus.UnsubscribeAsync<ReminderTriggeredEvent>(_inAppNotificationHandler, cancellationToken);
        }

        if (_calDeletedHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarEventDeletedEvent>(_calDeletedHandler, cancellationToken);

        if (_calUpdatedHandler is not null)
            await _eventBus.UnsubscribeAsync<CalendarEventUpdatedEvent>(_calUpdatedHandler, cancellationToken);
    }
}
