using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that prepares the server temp directory on startup and periodically
/// removes stale temp files. The directory is created with 700 permissions (Linux/macOS).
/// Runs immediately on startup, then every 6 hours.
/// </summary>
internal sealed class TempFileCleanupService : BackgroundService
{
    private const string ServiceName = "Temp File Cleanup";
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);

    private readonly string _tmpPath;
    private readonly ILogger<TempFileCleanupService> _logger;
    private readonly IBackgroundServiceTracker _tracker;

    /// <summary>
    /// Initializes a new instance of <see cref="TempFileCleanupService"/>.
    /// </summary>
    public TempFileCleanupService(IOptions<FileUploadOptions> options, ILogger<TempFileCleanupService> logger, IBackgroundServiceTracker tracker)
    {
        _tmpPath = options.Value.TmpPath ?? Path.GetTempPath();
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        RunTrackedCleanup("initial");

        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            RunTrackedCleanup("scheduled");
        }
    }

    private void RunTrackedCleanup(string trigger)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("{Service} cycle starting ({Trigger})", ServiceName, trigger);
            Cleanup();
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: true);
            _logger.LogInformation("{Service} cycle completed in {Elapsed:F1}s", ServiceName, sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, sw.Elapsed, success: false, message: ex.Message);
            _logger.LogError(ex, "Error during {Service}", ServiceName);
        }
    }

    private void Cleanup()
    {
        try
        {
            Directory.CreateDirectory(_tmpPath);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    _tmpPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var cutoff = DateTime.UtcNow.AddHours(-1);
            var deleted = 0;

            foreach (var file in Directory.GetFiles(_tmpPath))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Could not delete stale temp file {File}.", file);
                    }
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Cleaned up {Count} stale temp file(s) from {TmpPath}.", deleted, _tmpPath);
            }
            else
            {
                _logger.LogDebug("Temp directory ready: {TmpPath}.", _tmpPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize temp directory {TmpPath}.", _tmpPath);
        }
    }
}
