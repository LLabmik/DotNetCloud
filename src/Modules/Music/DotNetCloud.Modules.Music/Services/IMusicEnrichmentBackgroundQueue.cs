using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Queues post-scan music enrichment work to run outside the Blazor page lifecycle.
/// </summary>
public interface IMusicEnrichmentBackgroundQueue
{
    /// <summary>
    /// Attempts to enqueue a post-scan enrichment job.
    /// </summary>
    /// <returns><c>true</c> when the job was queued; otherwise <c>false</c> if one is already queued or running.</returns>
    ValueTask<bool> EnqueueAsync(MusicEnrichmentJob job, CancellationToken cancellationToken = default);
}

/// <summary>
/// Background enrichment request created after a music library scan completes.
/// </summary>
public sealed record MusicEnrichmentJob
{
    /// <summary>User whose library should be enriched.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Whether to fetch missing album art.</summary>
    public bool FetchAlbumArt { get; init; }

    /// <summary>Whether to enrich metadata.</summary>
    public bool FetchMetadata { get; init; }

    /// <summary>UTC time when the originating scan started.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Total files discovered by the scan.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Tracks added during the scan.</summary>
    public int TracksAdded { get; init; }

    /// <summary>Tracks updated during the scan.</summary>
    public int TracksUpdated { get; init; }

    /// <summary>Tracks skipped during the scan.</summary>
    public int TracksSkipped { get; init; }

    /// <summary>Tracks failed during the scan.</summary>
    public int TracksFailed { get; init; }

    /// <summary>Tracks removed during the scan.</summary>
    public int TracksRemoved { get; init; }
}