using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Video.Events;

/// <summary>
/// Published when a video is added to a series (franchise or TV episode).
/// </summary>
public sealed record VideoAddedToSeriesEvent : IEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>The series ID.</summary>
    public required Guid SeriesId { get; init; }

    /// <summary>The video ID.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>Season number if this is a TV episode, null for franchises.</summary>
    public int? SeasonNumber { get; init; }

    /// <summary>Episode number if this is a TV episode, null for franchises.</summary>
    public int? EpisodeNumber { get; init; }
}
