using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles <see cref="ChannelCreatedEvent"/> by notifying connected UI clients
/// so they add the new channel to their sidebar without a page refresh.
/// </summary>
public sealed class ChannelCreatedEventHandler : IEventHandler<ChannelCreatedEvent>
{
    private readonly IChatMessageNotifier _notifier;
    private readonly ILogger<ChannelCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelCreatedEventHandler"/> class.
    /// </summary>
    public ChannelCreatedEventHandler(IChatMessageNotifier notifier, ILogger<ChannelCreatedEventHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(ChannelCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Channel created: {ChannelName} ({ChannelType}) by user {UserId}",
            @event.ChannelName,
            @event.ChannelType,
            @event.CreatedByUserId);

        _notifier.NotifyChannelAdded(@event.ChannelId);
        return Task.CompletedTask;
    }
}
