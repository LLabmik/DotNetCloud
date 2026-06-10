namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Status of a transcoding job.
/// </summary>
public enum TranscodingJobStatus
{
    /// <summary>Job created but not yet started.</summary>
    Queued,

    /// <summary>ffmpeg is currently running.</summary>
    Running,

    /// <summary>ffmpeg completed successfully.</summary>
    Completed,

    /// <summary>ffmpeg exited with a non-zero code.</summary>
    Failed,

    /// <summary>Job was cancelled by the user or client disconnect.</summary>
    Cancelled
}

/// <summary>
/// Represents a single video transcoding job.
/// Used for tracking progress and lifecycle.
/// This is a simple DTO — thread-safe reads are expected via the tracker.
/// </summary>
public sealed class TranscodingJob
{
    /// <summary>Unique job identifier (GUID string).</summary>
    public required string Id { get; init; }

    /// <summary>The video entity ID being transcoded.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The user ID who requested the transcode.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Cache key for the transcode output.</summary>
    public required string CacheKey { get; init; }

    /// <summary>Path where ffmpeg is writing the output file.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Current job status.</summary>
    public TranscodingJobStatus Status { get; set; } = TranscodingJobStatus.Queued;

    /// <summary>Progress percentage (0.0 to 100.0).</summary>
    public double ProgressPercent { get; set; }

    /// <summary>Current transcode position in the source video.</summary>
    public TimeSpan CurrentTime { get; set; }

    /// <summary>ffmpeg speed multiplier (e.g., 1.5x = faster than real-time).</summary>
    public double Speed { get; set; }

    /// <summary>ffmpeg process ID for monitoring.</summary>
    public int ProcessId { get; set; }

    /// <summary>When the job was created (UTC).</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the job finished (completed, failed, or cancelled). Null if still running.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if status is Failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether this is an HLS transcode (vs. progressive MP4). HLS outputs segments + playlist.</summary>
    public bool IsHls { get; set; }
}
