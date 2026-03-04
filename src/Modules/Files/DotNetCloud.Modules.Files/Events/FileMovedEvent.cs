using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a file or folder is moved or renamed.
/// </summary>
public sealed record FileMovedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the moved file node.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The name of the moved node.</summary>
    public required string FileName { get; init; }

    /// <summary>The previous parent folder ID.</summary>
    public Guid? PreviousParentId { get; init; }

    /// <summary>The new parent folder ID.</summary>
    public Guid? NewParentId { get; init; }

    /// <summary>The user who moved the node.</summary>
    public required Guid MovedByUserId { get; init; }
}
