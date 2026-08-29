using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Background watchdog that cancels abandoned HLS transcode streams.
/// A stream is considered abandoned when a running HLS job has had no segment
/// or playlist requested for longer than <see cref="VideoTranscodingOptions.HlsIdleTimeoutSeconds"/>.
/// This is a safety net for cases where no client cancel signal arrives
/// (browser crash, network drop, tab killed before <c>pagehide</c> can fire).
/// </summary>
public sealed class HlsStreamWatchdog : BackgroundService
{
    private readonly TranscodingJobTracker _tracker;
    private readonly FfmpegProcessManager _processManager;
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<HlsStreamWatchdog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HlsStreamWatchdog"/> class.
    /// </summary>
    public HlsStreamWatchdog(
        TranscodingJobTracker tracker,
        FfmpegProcessManager processManager,
        VideoTranscodingOptions options,
        ILogger<HlsStreamWatchdog> logger)
    {
        _tracker = tracker;
        _processManager = processManager;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleTimeout = TimeSpan.FromSeconds(_options.HlsIdleTimeoutSeconds);
        var interval = TimeSpan.FromSeconds(_options.HlsWatchdogIntervalSeconds);

        if (idleTimeout <= TimeSpan.Zero || interval <= TimeSpan.Zero)
        {
            _logger.LogInformation(
                "HLS idle watchdog disabled (HlsIdleTimeoutSeconds={Timeout}, HlsWatchdogIntervalSeconds={Interval})",
                _options.HlsIdleTimeoutSeconds, _options.HlsWatchdogIntervalSeconds);
            return;
        }

        _logger.LogInformation("HLS idle watchdog started: interval={Interval}, idleTimeout={IdleTimeout}",
            interval, idleTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                var now = DateTime.UtcNow;
                foreach (var job in _tracker.GetActiveHlsJobs())
                {
                    if (job.Status != TranscodingJobStatus.Running)
                        continue;

                    if (!IsIdle(job, now, idleTimeout))
                        continue;

                    var idleFor = now - (job.LastSegmentRequestedAt ?? job.CreatedAt);
                    _logger.LogInformation(
                        "Cancelling idle HLS job {JobId} for video {VideoId} (no segment request for {Idle})",
                        job.Id, job.VideoId, idleFor);
                    _processManager.CancelJob(job.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HLS idle watchdog iteration failed");
            }
        }

        _logger.LogInformation("HLS idle watchdog stopped");
    }

    /// <summary>
    /// Determines whether an HLS job is idle — i.e. no segment/playlist has been requested
    /// within the timeout window. When no request has ever been recorded, the job creation
    /// time is used as the baseline. Pure helper so it can be unit-tested.
    /// </summary>
    /// <param name="job">The transcode job to evaluate.</param>
    /// <param name="now">The current UTC time.</param>
    /// <param name="idleTimeout">The idle threshold.</param>
    /// <returns>True when the job is considered abandoned.</returns>
    internal static bool IsIdle(TranscodingJob job, DateTime now, TimeSpan idleTimeout)
    {
        var lastRequest = job.LastSegmentRequestedAt ?? job.CreatedAt;
        return now - lastRequest > idleTimeout;
    }
}
