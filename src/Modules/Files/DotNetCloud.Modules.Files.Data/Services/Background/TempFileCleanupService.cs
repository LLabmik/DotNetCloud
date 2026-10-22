using System.Diagnostics;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that prepares the server temp directory on startup and periodically
/// removes stale temp files. Directories the service owns are created with 700 permissions
/// (Linux/macOS); the shared OS temp directory is never re-permissioned.
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

    internal void Cleanup()
    {
        try
        {
            Directory.CreateDirectory(_tmpPath);

            TryRestrictPermissions(_tmpPath);

            const long twentyFourHours = -24L;
            const long oneHour = -1L;

            var now = DateTime.UtcNow;
            var deleted = 0;

            foreach (var file in Directory.GetFiles(_tmpPath))
            {
                var fileName = Path.GetFileName(file);

                // Album ZIPs: 24-hour retention
                var cutoff = fileName.StartsWith("dotnetcloud-album-", StringComparison.Ordinal)
                    ? now.AddHours(twentyFourHours)
                    : now.AddHours(oneHour);

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

    /// <summary>
    /// Whether the configured temp path is the operating system's shared temp directory.
    /// </summary>
    /// <remarks>
    /// The OS temp root (<c>/tmp</c> on Linux/macOS) is owned by root and shared with every other
    /// process on the host. It must never be chmod-ed: the call fails with
    /// <see cref="UnauthorizedAccessException"/> on GitHub runners, and on hosts where the service
    /// user does own it, it would lock every other process out of the temp directory.
    /// </remarks>
    /// <param name="path">Directory to test.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is the shared temp root.</returns>
    internal static bool IsSharedTempRoot(string path)
    {
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var sharedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));

        return string.Equals(candidate, sharedRoot, StringComparison.Ordinal);
    }

    /// <summary>
    /// Restricts a service-owned temp directory to the service user, tolerating hosts that do not
    /// permit the change. Never touches the shared OS temp root and never aborts the cleanup pass.
    /// </summary>
    /// <param name="path">Temp directory to tighten.</param>
    private void TryRestrictPermissions(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        if (IsSharedTempRoot(path))
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Could not restrict permissions on temp directory {TmpPath}.", path);
        }
    }
}
