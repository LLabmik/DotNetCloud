using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Events;

/// <summary>
/// Handles <see cref="ConversationCreatedEvent"/> for logging and auditing.
/// </summary>
public sealed class ConversationCreatedEventHandler : IEventHandler<ConversationCreatedEvent>
{
    private readonly ILogger<ConversationCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationCreatedEventHandler"/> class.
    /// </summary>
    public ConversationCreatedEventHandler(ILogger<ConversationCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(ConversationCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AI conversation created: {ConversationId} by user {UserId} using model {Model}",
            @event.ConversationId,
            @event.OwnerId,
            @event.Model);

        return Task.CompletedTask;
    }
}
