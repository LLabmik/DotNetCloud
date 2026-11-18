namespace DotNetCloud.Core.Events;

/// <summary>
/// Published when a file or folder is moved to the trash.
/// </summary>
/// <remarks>
/// This contract lives in <c>DotNetCloud.Core</c> rather than in the Files module because any module
/// (first-party or third-party) may subscribe to it, and a module must never reference another
/// module's assembly to obtain an event shape.
/// </remarks>
public sealed record FileDeletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the deleted file node.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The name of the deleted node.</summary>
    public required string FileName { get; init; }

    /// <summary>The user who deleted the node.</summary>
    public required Guid DeletedByUserId { get; init; }

    /// <summary>Whether this was a permanent deletion or a trash operation.</summary>
    public bool IsPermanent { get; init; }
}
