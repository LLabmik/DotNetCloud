using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Chat.Data.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Subscribes chat event handlers on startup and unsubscribes them on shutdown.
/// Bridges domain events published by chat services to the in-process
/// <see cref="IChatMessageNotifier"/> so Blazor UI circuits receive real-time updates.
/// </summary>
internal sealed class ChatEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IChatMessageNotifier _notifier;
    private readonly IChannelMemberService _memberService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserDirectory _userDirectory;
    private readonly ILoggerFactory _loggerFactory;
    private ChannelCreatedEventHandler? _channelCreatedHandler;
    private ChannelDeletedEventHandler? _channelDeletedHandler;
    private DmChannelCreatedEventHandler? _dmChannelCreatedHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="ChatEventSubscriber"/>.
    /// </summary>
    public ChatEventSubscriber(
        IEventBus eventBus,
        IChatMessageNotifier notifier,
        IChannelMemberService memberService,
        IPushNotificationService pushService,
        IUserDirectory userDirectory,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _notifier = notifier;
        _memberService = memberService;
        _pushService = pushService;
        _userDirectory = userDirectory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _channelCreatedHandler = new ChannelCreatedEventHandler(
            _notifier,
            _loggerFactory.CreateLogger<ChannelCreatedEventHandler>());

        _channelDeletedHandler = new ChannelDeletedEventHandler(
            _notifier,
            _loggerFactory.CreateLogger<ChannelDeletedEventHandler>());

        _dmChannelCreatedHandler = new DmChannelCreatedEventHandler(
            _memberService,
            _pushService,
            _notifier,
            _userDirectory,
            _loggerFactory.CreateLogger<DmChannelCreatedEventHandler>());

        await _eventBus.SubscribeAsync(_channelCreatedHandler, cancellationToken);
        await _eventBus.SubscribeAsync(_channelDeletedHandler, cancellationToken);
        await _eventBus.SubscribeAsync(_dmChannelCreatedHandler, cancellationToken);

        _loggerFactory.CreateLogger<ChatEventSubscriber>()
            .LogInformation("Chat event handlers subscribed (ChannelCreated, ChannelDeleted, DmChannelCreated)");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channelCreatedHandler is not null)
            await _eventBus.UnsubscribeAsync(_channelCreatedHandler, cancellationToken);

        if (_channelDeletedHandler is not null)
            await _eventBus.UnsubscribeAsync(_channelDeletedHandler, cancellationToken);

        if (_dmChannelCreatedHandler is not null)
            await _eventBus.UnsubscribeAsync(_dmChannelCreatedHandler, cancellationToken);
    }
}
