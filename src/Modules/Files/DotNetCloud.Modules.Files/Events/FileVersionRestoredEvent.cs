using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a file is restored to a previous version.
/// Restoring creates a new version with the old content rather than reverting in place.
/// </summary>
public sealed record FileVersionRestoredEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the file node whose version was restored.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The name of the file.</summary>
    public required string FileName { get; init; }

    /// <summary>The ID of the source version that was restored from.</summary>
    public required Guid SourceVersionId { get; init; }

    /// <summary>The version number that was restored from.</summary>
    public int SourceVersionNumber { get; init; }

    /// <summary>The new version number created as the restore result.</summary>
    public int NewVersionNumber { get; init; }

    /// <summary>The user who performed the restore.</summary>
    public required Guid RestoredByUserId { get; init; }
}
