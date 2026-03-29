namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new board is created.
/// </summary>
public sealed record BoardCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the newly created board.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The title of the board.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The ID of the user who created the board.
    /// </summary>
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when a board is deleted.
/// </summary>
public sealed record BoardDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted board.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The ID of the user who deleted the board.
    /// </summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>
    /// Whether this was a permanent (hard) delete vs. soft delete.
    /// </summary>
    public bool IsPermanent { get; init; }
}

/// <summary>
/// Raised when a new card is created on a board.
/// </summary>
public sealed record CardCreatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the newly created card.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The title of the card.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The list the card was created in.
    /// </summary>
    public required Guid ListId { get; init; }

    /// <summary>
    /// The ID of the user who created the card.
    /// </summary>
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Raised when a card is moved to a different list or position.
/// </summary>
public sealed record CardMovedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the card that was moved.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The list the card was moved from.
    /// </summary>
    public required Guid FromListId { get; init; }

    /// <summary>
    /// The list the card was moved to.
    /// </summary>
    public required Guid ToListId { get; init; }

    /// <summary>
    /// The ID of the user who moved the card.
    /// </summary>
    public required Guid MovedByUserId { get; init; }
}

/// <summary>
/// Raised when a card is updated (title, description, priority, due date, etc.).
/// </summary>
public sealed record CardUpdatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the updated card.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The ID of the user who updated the card.
    /// </summary>
    public required Guid UpdatedByUserId { get; init; }
}

/// <summary>
/// Raised when a card is deleted.
/// </summary>
public sealed record CardDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the deleted card.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belonged to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The ID of the user who deleted the card.
    /// </summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>
    /// Whether this was a permanent (hard) delete vs. soft delete.
    /// </summary>
    public bool IsPermanent { get; init; }
}

/// <summary>
/// Raised when a user is assigned to a card.
/// </summary>
public sealed record CardAssignedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The card the user was assigned to.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The ID of the user who was assigned.
    /// </summary>
    public required Guid AssignedUserId { get; init; }

    /// <summary>
    /// The ID of the user who made the assignment.
    /// </summary>
    public required Guid AssignedByUserId { get; init; }
}

/// <summary>
/// Raised when a comment is added to a card.
/// </summary>
public sealed record CardCommentAddedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the comment.
    /// </summary>
    public required Guid CommentId { get; init; }

    /// <summary>
    /// The card the comment was added to.
    /// </summary>
    public required Guid CardId { get; init; }

    /// <summary>
    /// The board the card belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The ID of the user who added the comment.
    /// </summary>
    public required Guid UserId { get; init; }
}

/// <summary>
/// Raised when a sprint is started.
/// </summary>
public sealed record SprintStartedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the sprint.
    /// </summary>
    public required Guid SprintId { get; init; }

    /// <summary>
    /// The board the sprint belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The title of the sprint.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The ID of the user who started the sprint.
    /// </summary>
    public required Guid StartedByUserId { get; init; }
}

/// <summary>
/// Raised when a sprint is completed.
/// </summary>
public sealed record SprintCompletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// The ID of the sprint.
    /// </summary>
    public required Guid SprintId { get; init; }

    /// <summary>
    /// The board the sprint belongs to.
    /// </summary>
    public required Guid BoardId { get; init; }

    /// <summary>
    /// The title of the sprint.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The ID of the user who completed the sprint.
    /// </summary>
    public required Guid CompletedByUserId { get; init; }

    /// <summary>
    /// Number of cards completed in the sprint.
    /// </summary>
    public int CompletedCardCount { get; init; }

    /// <summary>
    /// Total number of cards that were in the sprint.
    /// </summary>
    public int TotalCardCount { get; init; }
}
