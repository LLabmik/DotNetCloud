using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a file or folder is restored from the trash.
/// </summary>
public sealed record FileRestoredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the restored file node.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The name of the restored node.</summary>
    public required string FileName { get; init; }

    /// <summary>The parent folder ID after restoration.</summary>
    public Guid? RestoredToParentId { get; init; }

    /// <summary>The user who restored the node.</summary>
    public required Guid RestoredByUserId { get; init; }
}
