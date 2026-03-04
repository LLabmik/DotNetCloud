using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Events;

/// <summary>
/// Handles <see cref="ChannelCreatedEvent"/> by logging the channel creation.
/// </summary>
public sealed class ChannelCreatedEventHandler : IEventHandler<ChannelCreatedEvent>
{
    private readonly ILogger<ChannelCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelCreatedEventHandler"/> class.
    /// </summary>
    public ChannelCreatedEventHandler(ILogger<ChannelCreatedEventHandler> logger)
    {
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

        return Task.CompletedTask;
    }
}
