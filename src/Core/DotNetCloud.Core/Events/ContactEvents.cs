namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new contact is created.
/// </summary>
public sealed record ContactCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the newly created contact.
    /// </summary>
    public required Guid ContactId { get; init; }

    /// <summary>
    /// The display name of the contact.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The ID of the user who owns the contact.
    /// </summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when an existing contact is updated.
/// </summary>
public sealed record ContactUpdatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the updated contact.
    /// </summary>
    public required Guid ContactId { get; init; }

    /// <summary>
    /// The ID of the user who updated the contact.
    /// </summary>
    public required Guid UpdatedByUserId { get; init; }
}

/// <summary>
/// Raised when a contact is deleted (soft or hard).
/// </summary>
public sealed record ContactDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted contact.
    /// </summary>
    public required Guid ContactId { get; init; }

    /// <summary>
    /// The ID of the user who deleted the contact.
    /// </summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>
    /// Whether this was a permanent (hard) delete vs. soft delete.
    /// </summary>
    public bool IsPermanent { get; init; }
}
