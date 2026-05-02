using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Events;

/// <summary>
/// Handles <see cref="EmailAccountAddedEvent"/> for the Email module.
/// </summary>
public sealed class EmailAccountAddedEventHandler : IEventHandler<EmailAccountAddedEvent>
{
    private readonly ILogger<EmailAccountAddedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAccountAddedEventHandler"/> class.
    /// </summary>
    public EmailAccountAddedEventHandler(ILogger<EmailAccountAddedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(EmailAccountAddedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email account added: {AccountId} ({EmailAddress})", @event.AccountId, @event.EmailAddress);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles <see cref="EmailMessageReceivedEvent"/> for the Email module.
/// </summary>
public sealed class EmailMessageReceivedEventHandler : IEventHandler<EmailMessageReceivedEvent>
{
    private readonly ILogger<EmailMessageReceivedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailMessageReceivedEventHandler"/> class.
    /// </summary>
    public EmailMessageReceivedEventHandler(ILogger<EmailMessageReceivedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(EmailMessageReceivedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email message received: {MessageId} '{Subject}' from {From}", @event.MessageId, @event.Subject, @event.From);
        return Task.CompletedTask;
    }
}
