using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Bookmarks;

/// <summary>
/// Published when a bookmark is updated.
/// </summary>
public sealed record BookmarkUpdatedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the updated bookmark.</summary>
    public required Guid BookmarkId { get; init; }

    /// <summary>The owner of the bookmark.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>The updated title.</summary>
    public required string Title { get; init; }
}
