using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a file or folder is shared with a user, team, or via public link.
/// </summary>
public sealed record FileSharedEvent : IEvent
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

    /// <summary>The type of share (User, Team, Group, PublicLink).</summary>
    public required string ShareType { get; init; }

    /// <summary>The user the file was shared with (for user shares).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>The user who created the share.</summary>
    public required Guid SharedByUserId { get; init; }
}
