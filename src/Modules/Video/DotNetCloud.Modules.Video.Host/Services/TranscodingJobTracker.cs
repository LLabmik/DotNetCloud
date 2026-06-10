using System.Collections.Concurrent;
using DotNetCloud.Modules.Video.Services;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Thread-safe in-memory tracker for active and recent transcoding jobs.
/// Registered as singleton.
/// </summary>
public sealed class TranscodingJobTracker
{
    private readonly ConcurrentDictionary<string, TranscodingJob> _jobs = new();

    /// <summary>
    /// Creates and registers a new job. Returns the job.
    /// </summary>
    public TranscodingJob CreateJob(Guid videoId, Guid userId, string cacheKey)
    {
        var job = new TranscodingJob
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = videoId,
            UserId = userId,
            CacheKey = cacheKey
        };
        _jobs[job.Id] = job;
        return job;
    }

    /// <summary>
    /// Gets a job by ID. Returns null if not found.
    /// </summary>
    public TranscodingJob? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <summary>
    /// Gets the active (running or queued) job for a given video+user pair, if any.
    /// Returns null if no active job exists.
    /// </summary>
    public TranscodingJob? GetActiveJob(Guid videoId, Guid userId)
    {
        return _jobs.Values.FirstOrDefault(j =>
            j.VideoId == videoId &&
            j.UserId == userId &&
            (j.Status == TranscodingJobStatus.Queued || j.Status == TranscodingJobStatus.Running));
    }

    /// <summary>
    /// Removes old completed/failed/cancelled jobs older than the given age.
    /// </summary>
    public void PurgeOldJobs(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var kvp in _jobs)
        {
            if (kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt.Value < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Gets an active (running or queued) HLS transcode job for a video, regardless of user.
    /// Used by the HLS segment endpoint to find the output directory.
    /// </summary>
    public TranscodingJob? GetActiveHlsJob(Guid videoId)
    {
        return _jobs.Values.FirstOrDefault(j =>
            j.VideoId == videoId &&
            j.IsHls &&
            (j.Status == TranscodingJobStatus.Queued || j.Status == TranscodingJobStatus.Running));
    }
}
