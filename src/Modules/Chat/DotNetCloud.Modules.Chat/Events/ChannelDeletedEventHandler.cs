using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles <see cref="ChannelDeletedEvent"/> by notifying connected UI clients
/// so they remove the channel from their sidebar without a page refresh.
/// </summary>
public sealed class ChannelDeletedEventHandler : IEventHandler<ChannelDeletedEvent>
{
    private readonly IChatMessageNotifier _notifier;
    private readonly ILogger<ChannelDeletedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelDeletedEventHandler"/> class.
    /// </summary>
    public ChannelDeletedEventHandler(IChatMessageNotifier notifier, ILogger<ChannelDeletedEventHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(ChannelDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Channel deleted: {ChannelName} ({ChannelId}) by user {UserId}",
            @event.ChannelName,
            @event.ChannelId,
            @event.DeletedByUserId);

        _notifier.NotifyChannelDeleted(@event.ChannelId);
        return Task.CompletedTask;
    }
}
