namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// FIFO queue for background video enrichment jobs. One active job per user at a time.
/// </summary>
public interface IVideoEnrichmentBackgroundQueue
{
    /// <summary>
    /// Enqueues an enrichment job. Returns false if a job is already queued for the same user.
    /// </summary>
    ValueTask<bool> EnqueueAsync(VideoEnrichmentJob job, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data carrier for a background video enrichment job.
/// </summary>
public sealed record VideoEnrichmentJob
{
    public required Guid OwnerId { get; init; }
    public bool FetchPosters { get; init; }
    public bool FetchMetadata { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public int TotalFiles { get; init; }
    public int VideosAdded { get; init; }
    public int VideosSkipped { get; init; }
    public int VideosFailed { get; init; }
    public int VideosRemoved { get; init; }

    /// <summary>
    /// When set, only these user-video IDs are enriched (a scoped fast-track job).
    /// When null or empty, all pending videos for the owner are enriched.
    /// </summary>
    public IReadOnlyList<Guid>? VideoIds { get; init; }

    /// <summary>
    /// Marks a fast-track job (small batch of newly added videos). Fast-track jobs
    /// run without scan-progress reporting, never touch the user's scan cancellation
    /// token, and skip the final series-enrichment pass — they are quick background
    /// enrichment only.
    /// </summary>
    public bool IsFastTrack { get; init; }
}
