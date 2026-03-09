using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.HealthChecks;

/// <summary>
/// Hosted service that logs Linux resource status (inotify watch limit, inode availability)
/// on startup and then every 30 minutes. Runs silently on non-Linux platforms.
/// </summary>
public sealed class LinuxResourceMonitorService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    private readonly string _dataDir;
    private readonly ILogger<LinuxResourceMonitorService> _logger;

    /// <summary>Initialises a new instance of <see cref="LinuxResourceMonitorService"/>.</summary>
    public LinuxResourceMonitorService(string dataDir, ILogger<LinuxResourceMonitorService> logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Run immediately on startup, then every 30 minutes.
        while (!stoppingToken.IsCancellationRequested)
        {
            LogResourceStatus();

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void LogResourceStatus()
    {
        // inotify watch limit
        int watchLimit = LinuxResourceHealthCheck.ReadInotifyWatchLimit();
        if (watchLimit > 0)
        {
            if (watchLimit < LinuxResourceHealthCheck.MinRecommendedWatches)
            {
                _logger.LogWarning(
                    "inotify.watch_limit.low Limit={WatchLimit} Recommended={Recommended} " +
                    "Fix: echo 'fs.inotify.max_user_watches={Recommended}' | sudo tee /etc/sysctl.d/50-dotnetcloud.conf && sudo sysctl --system",
                    watchLimit, LinuxResourceHealthCheck.MinRecommendedWatches, LinuxResourceHealthCheck.MinRecommendedWatches);
            }
            else
            {
                _logger.LogInformation("inotify.watch_limit Limit={WatchLimit}", watchLimit);
            }
        }

        // inode availability
        if (LinuxResourceHealthCheck.TryGetInodeInfo(_dataDir, out long totalInodes, out long freeInodes))
        {
            double freePercent = totalInodes > 0 ? (double)freeInodes / totalInodes * 100.0 : 100.0;

            if (freePercent < LinuxResourceHealthCheck.InodeUnhealthyThreshold * 100)
            {
                _logger.LogError(
                    "inode.critical DataDir={DataDir} Total={Total} Free={Free} FreePercent={FreePercent:F1}% — filesystem inode limit nearly exhausted; new files cannot be created",
                    _dataDir, totalInodes, freeInodes, freePercent);
            }
            else if (freePercent < LinuxResourceHealthCheck.InodeDegradedThreshold * 100)
            {
                _logger.LogWarning(
                    "inode.low DataDir={DataDir} Total={Total} Free={Free} FreePercent={FreePercent:F1}%",
                    _dataDir, totalInodes, freeInodes, freePercent);
            }
            else
            {
                _logger.LogInformation(
                    "inode.status DataDir={DataDir} Total={Total} Free={Free} FreePercent={FreePercent:F1}%",
                    _dataDir, totalInodes, freeInodes, freePercent);
            }
        }
    }
}
