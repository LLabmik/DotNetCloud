using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a public link is accessed for the first time (download count transitions from 0 to 1).
/// </summary>
public sealed record PublicLinkAccessedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the shared file node.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The name of the shared node.</summary>
    public required string FileName { get; init; }

    /// <summary>The share ID.</summary>
    public required Guid ShareId { get; init; }

    /// <summary>The user who created the public link share.</summary>
    public required Guid CreatedByUserId { get; init; }
}
