using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Events;

/// <summary>
/// Raised when a new Product is created.
/// </summary>
public sealed record ProductCreatedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid OwnerId { get; init; }
}

/// <summary>
/// Raised when a Product is deleted.
/// </summary>
public sealed record ProductDeletedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid ProductId { get; init; }
}

/// <summary>
/// Raised when a new WorkItem is created.
/// </summary>
public sealed record WorkItemCreatedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid ProductId { get; init; }
    public required WorkItemType Type { get; init; }
    public Guid? ParentWorkItemId { get; init; }
}

/// <summary>
/// Raised when a WorkItem is updated.
/// </summary>
public sealed record WorkItemUpdatedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required WorkItemType Type { get; init; }
}

/// <summary>
/// Raised when a WorkItem is moved between swimlanes.
/// </summary>
public sealed record WorkItemMovedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required WorkItemType Type { get; init; }
    public required Guid FromSwimlaneId { get; init; }
    public required Guid ToSwimlaneId { get; init; }
}

/// <summary>
/// Raised when a WorkItem is deleted.
/// </summary>
public sealed record WorkItemDeletedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required WorkItemType Type { get; init; }
}

/// <summary>
/// Raised when a user is assigned to a WorkItem.
/// </summary>
public sealed record WorkItemAssignedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid UserId { get; init; }
}

/// <summary>
/// Raised when a comment is added to a WorkItem.
/// </summary>
public sealed record WorkItemCommentAddedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid CommentId { get; init; }
    public required Guid UserId { get; init; }
}

/// <summary>
/// Raised when a sprint is started.
/// </summary>
public sealed record SprintStartedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SprintId { get; init; }
    public required Guid EpicId { get; init; }
}

/// <summary>
/// Raised when a sprint is completed.
/// </summary>
public sealed record SprintCompletedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SprintId { get; init; }
    public required Guid EpicId { get; init; }
}

/// <summary>
/// Raised when a planning poker session is started.
/// </summary>
public sealed record PokerSessionStartedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid EpicId { get; init; }
    public required Guid ItemId { get; init; }
}

/// <summary>
/// Raised when poker session votes are revealed.
/// </summary>
public sealed record PokerSessionRevealedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid EpicId { get; init; }
}

/// <summary>
/// Raised when a poker session is completed.
/// </summary>
public sealed record PokerSessionCompletedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid EpicId { get; init; }
}

/// <summary>
/// Raised when a new team is created.
/// </summary>
public sealed record TeamCreatedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid TeamId { get; init; }
    public required string Name { get; init; }
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Raised when a team is deleted.
/// </summary>
public sealed record TeamDeletedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid TeamId { get; init; }
    public required Guid DeletedByUserId { get; init; }
}
