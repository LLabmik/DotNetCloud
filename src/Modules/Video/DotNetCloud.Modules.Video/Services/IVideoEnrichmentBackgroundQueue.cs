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
}
