using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Email;

/// <summary>
/// Base account event with common fields.
/// </summary>
public abstract record EmailAccountEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The account ID.</summary>
    public required Guid AccountId { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Published when a new email account is added.
/// </summary>
public sealed record EmailAccountAddedEvent : EmailAccountEvent
{
    /// <summary>Provider type (ImapSmtp or Gmail).</summary>
    public required string ProviderType { get; init; }

    /// <summary>Email address of the account.</summary>
    public required string EmailAddress { get; init; }
}

/// <summary>
/// Published when an email account is removed.
/// </summary>
public sealed record EmailAccountRemovedEvent : EmailAccountEvent
{
    /// <summary>Email address of the removed account.</summary>
    public required string EmailAddress { get; init; }
}

/// <summary>
/// Published when a new email thread is created.
/// </summary>
public sealed record EmailThreadCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The thread ID.</summary>
    public required Guid ThreadId { get; init; }

    /// <summary>The account ID.</summary>
    public required Guid AccountId { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The thread subject.</summary>
    public required string Subject { get; init; }

    /// <summary>Number of messages in the thread.</summary>
    public required int MessageCount { get; init; }
}

/// <summary>
/// Published when a new email message is received/synced.
/// </summary>
public sealed record EmailMessageReceivedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The message ID.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The thread ID.</summary>
    public required Guid ThreadId { get; init; }

    /// <summary>The account ID.</summary>
    public required Guid AccountId { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Message subject.</summary>
    public required string Subject { get; init; }

    /// <summary>Sender display string.</summary>
    public required string From { get; init; }
}

/// <summary>
/// Published when an email is sent successfully.
/// </summary>
public sealed record EmailSentEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The account ID used to send.</summary>
    public required Guid AccountId { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The sent message subject.</summary>
    public required string Subject { get; init; }

    /// <summary>Recipients.</summary>
    public required IReadOnlyList<string> To { get; init; }
}

/// <summary>
/// Published when an email rule is triggered.
/// </summary>
public sealed record EmailRuleTriggeredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The rule ID that was triggered.</summary>
    public required Guid RuleId { get; init; }

    /// <summary>The message ID that triggered the rule.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Actions that were executed.</summary>
    public required IReadOnlyList<string> ExecutedActions { get; init; }
}
