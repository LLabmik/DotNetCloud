using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a share is about to expire (within the configured notification window).
/// </summary>
public sealed record ShareExpiringEvent : IEvent
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

    /// <summary>The user who created the share.</summary>
    public required Guid CreatedByUserId { get; init; }

    /// <summary>When the share expires (UTC).</summary>
    public required DateTime ExpiresAt { get; init; }
}
