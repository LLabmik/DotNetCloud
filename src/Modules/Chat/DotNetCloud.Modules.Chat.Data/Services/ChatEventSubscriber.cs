using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    private readonly ILoggerFactory _loggerFactory;
    private ChannelCreatedEventHandler? _channelCreatedHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="ChatEventSubscriber"/>.
    /// </summary>
    public ChatEventSubscriber(
        IEventBus eventBus,
        IChatMessageNotifier notifier,
        ILoggerFactory loggerFactory)
    {
        _eventBus = eventBus;
        _notifier = notifier;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _channelCreatedHandler = new ChannelCreatedEventHandler(
            _notifier,
            _loggerFactory.CreateLogger<ChannelCreatedEventHandler>());

        await _eventBus.SubscribeAsync(_channelCreatedHandler, cancellationToken);

        _loggerFactory.CreateLogger<ChatEventSubscriber>()
            .LogInformation("Chat event handlers subscribed (ChannelCreated)");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channelCreatedHandler is not null)
            await _eventBus.UnsubscribeAsync(_channelCreatedHandler, cancellationToken);
    }
}
