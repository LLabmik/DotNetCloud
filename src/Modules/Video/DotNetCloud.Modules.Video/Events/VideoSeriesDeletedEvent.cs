using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Published when a video series is deleted.
/// </summary>
public sealed record VideoSeriesDeletedEvent : IEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>The series ID.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>Series name.</summary>
    public required string Name { get; init; }
}
