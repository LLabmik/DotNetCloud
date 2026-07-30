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
    /// Per-video locks to prevent concurrent TranscodeHlsAsync calls (different HTTP requests)
    /// from spawning duplicate ffmpeg processes for the same video.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _hlsLocks = new();

    /// <summary>
    /// Creates and registers a new job. Returns the job.
    /// </summary>
    public TranscodingJob CreateJob(Guid videoId, Guid userId, string cacheKey)
    {
        var job = new TranscodingJob
        {
            Id = Guid.CreateVersion7().ToString("N"),
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
    /// Also returns completed jobs so that subsequent requests can reuse them
    /// instead of starting a redundant transcode.
    /// </summary>
    public TranscodingJob? GetActiveHlsJob(Guid videoId)
    {
        return _jobs.Values.FirstOrDefault(j =>
            j.VideoId == videoId &&
            j.IsHls &&
            (j.Status == TranscodingJobStatus.Queued || j.Status == TranscodingJobStatus.Running || j.Status == TranscodingJobStatus.Completed));
    }

    /// <summary>
    /// Acquires an exclusive per-video lock for HLS transcode creation.
    /// This prevents concurrent HTTP requests from spawning duplicate ffmpeg processes.
    /// Caller MUST release the lock via <see cref="ReleaseHlsLock"/> when done with
    /// the check-and-create critical section.
    /// </summary>
    /// <returns>A disposable that releases the lock, or null if the video already has an active job.</returns>
    public async Task<IDisposable?> AcquireHlsLockAsync(Guid videoId)
    {
        var semaphore = _hlsLocks.GetOrAdd(videoId, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(30));
        if (!acquired)
        {
            _logger?.LogWarning("HLS lock acquire timed out for video {VideoId}", videoId);
            return null;
        }

        // Double-check: another request might have created a job while we were waiting.
        // Also check for completed jobs to avoid re-transcoding already-done work.
        var existing = _jobs.Values.FirstOrDefault(j =>
            j.VideoId == videoId &&
            j.IsHls &&
            (j.Status == TranscodingJobStatus.Queued || j.Status == TranscodingJobStatus.Running || j.Status == TranscodingJobStatus.Completed));

        if (existing is not null)
        {
            semaphore.Release();
            // Clean up unused semaphore if possible
            TryCleanupLock(videoId, semaphore);
            return null; // Caller should retry GetActiveHlsJob
        }

        return new HlsLockReleaser(semaphore, () => TryCleanupLock(videoId, semaphore));
    }

    /// <summary>
    /// Releases a per-video HLS lock acquired by a previous call that returned null
    /// (meaning the job already existed). Use the disposable from AcquireHlsLockAsync instead.
    /// </summary>
    public void ReleaseHlsLock(Guid videoId)
    {
        if (_hlsLocks.TryGetValue(videoId, out var semaphore))
        {
            try
            { semaphore.Release(); }
            catch (SemaphoreFullException) { }
            TryCleanupLock(videoId, semaphore);
        }
    }

    /// <summary>
    /// Acquires the per-video HLS lock unconditionally (no "existing job" short-circuit),
    /// waiting for any in-progress job creation/refresh to finish first. Used by seek
    /// operations, which need exclusive access across cancelling the old job, deleting
    /// its output directory, and registering the new (seeked) job as a single atomic
    /// unit — otherwise a concurrent ordinary playlist-refresh request (no seek) can
    /// race in between and create its own unseeked job, which the seek would then
    /// mistakenly reuse via GetActiveHlsJob/IsJobReusable.
    /// </summary>
    /// <returns>A disposable that releases the lock, or null if the wait timed out.</returns>
    public async Task<IDisposable?> AcquireHlsLockExclusiveAsync(Guid videoId, TimeSpan? timeout = null)
    {
        var semaphore = _hlsLocks.GetOrAdd(videoId, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(timeout ?? TimeSpan.FromSeconds(30));
        if (!acquired)
            return null;

        return new HlsLockReleaser(semaphore, () => TryCleanupLock(videoId, semaphore));
    }

    private void TryCleanupLock(Guid videoId, SemaphoreSlim semaphore)
    {
        // Only remove the semaphore from the dictionary if no one is waiting
        if (semaphore.CurrentCount > 0)
        {
            _hlsLocks.TryRemove(videoId, out _);
        }
    }

    private sealed class HlsLockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Action _cleanup;

        public HlsLockReleaser(SemaphoreSlim semaphore, Action cleanup)
        {
            _semaphore = semaphore;
            _cleanup = cleanup;
        }

        public void Dispose()
        {
            try
            { _semaphore.Release(); }
            catch (SemaphoreFullException) { }
            _cleanup();
        }
    }

    private Microsoft.Extensions.Logging.ILogger<TranscodingJobTracker>? _logger;

    /// <summary>
    /// Sets a logger for diagnostic output (optional, set via DI).
    /// </summary>
    public void SetLogger(Microsoft.Extensions.Logging.ILogger<TranscodingJobTracker> logger)
    {
        _logger = logger;
    }
}
