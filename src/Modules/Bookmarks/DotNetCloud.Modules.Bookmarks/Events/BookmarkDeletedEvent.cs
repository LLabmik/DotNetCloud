using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Bookmarks;

/// <summary>
/// Published when a bookmark is deleted.
/// </summary>
public sealed record BookmarkDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted bookmark.</summary>
    public required Guid BookmarkId { get; init; }

    /// <summary>The owner of the bookmark.</summary>
    public required Guid OwnerId { get; init; }
}
