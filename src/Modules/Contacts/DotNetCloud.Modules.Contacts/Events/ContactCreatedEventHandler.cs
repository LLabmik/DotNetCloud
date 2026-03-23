using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Events;

/// <summary>
/// Handles <see cref="ContactCreatedEvent"/> within the Contacts module.
/// </summary>
public sealed class ContactCreatedEventHandler : IEventHandler<ContactCreatedEvent>
{
    private readonly ILogger<ContactCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCreatedEventHandler"/> class.
    /// </summary>
    public ContactCreatedEventHandler(ILogger<ContactCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(ContactCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Contact created: {ContactId} '{DisplayName}' by user {UserId}",
            @event.ContactId,
            @event.DisplayName,
            @event.OwnerId);

        return Task.CompletedTask;
    }
}
