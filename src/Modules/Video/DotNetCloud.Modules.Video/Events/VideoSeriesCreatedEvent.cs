using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Published when a new video series is created.
/// </summary>
public sealed record VideoSeriesCreatedEvent : IEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>The series ID.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>Series name.</summary>
    public required string Name { get; init; }

    /// <summary>Series type.</summary>
    public required string Type { get; init; }

    /// <summary>The owner user ID.</summary>
    public required Guid OwnerId { get; init; }
}
