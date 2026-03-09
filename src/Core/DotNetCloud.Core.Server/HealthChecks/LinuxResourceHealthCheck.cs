using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.HealthChecks;

/// <summary>
/// Health check that validates Linux-specific resource limits relevant to file-serving:
/// <list type="bullet">
///   <item>inotify watch limit (warns if below recommended minimum)</item>
///   <item>Filesystem inode availability on the data directory's mount point</item>
/// </list>
/// Skipped automatically on non-Linux platforms.
/// </summary>
public sealed partial class LinuxResourceHealthCheck : IHealthCheck
{
    /// <summary>Minimum recommended inotify max_user_watches value.</summary>
    internal const int MinRecommendedWatches = 65536;

    /// <summary>Inode percentage below which status is <c>Degraded</c>.</summary>
    internal const double InodeDegradedThreshold = 0.10;

    /// <summary>Inode percentage below which status is <c>Unhealthy</c>.</summary>
    internal const double InodeUnhealthyThreshold = 0.02;

    private readonly string _dataDir;
    private readonly ILogger<LinuxResourceHealthCheck> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="LinuxResourceHealthCheck"/>.
    /// </summary>
    /// <param name="dataDir">Path to the server data directory (checked for inode usage).</param>
    /// <param name="logger">Logger.</param>
    public LinuxResourceHealthCheck(string dataDir, ILogger<LinuxResourceHealthCheck> logger)
    {
        _dataDir = dataDir;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
            return Task.FromResult(HealthCheckResult.Healthy("Not Linux — resource checks skipped."));

        var data = new Dictionary<string, object>();
        var issues = new List<string>();
        var degraded = false;
        var unhealthy = false;

        // --- inotify watch limit ---
        int watchLimit = ReadInotifyWatchLimit();
        data["inotify_max_user_watches"] = watchLimit;

        if (watchLimit > 0 && watchLimit < MinRecommendedWatches)
        {
            degraded = true;
            issues.Add($"inotify max_user_watches={watchLimit} is below recommended {MinRecommendedWatches}. " +
                       $"Run: echo 'fs.inotify.max_user_watches={MinRecommendedWatches}' | sudo tee /etc/sysctl.d/50-dotnetcloud.conf && sudo sysctl --system");
        }

        // --- inode availability ---
        if (TryGetInodeInfo(_dataDir, out long totalInodes, out long freeInodes))
        {
            double freePercent = totalInodes > 0 ? (double)freeInodes / totalInodes : 1.0;
            data["inode_total"] = totalInodes;
            data["inode_free"] = freeInodes;
            data["inode_free_percent"] = Math.Round(freePercent * 100, 1);

            if (freePercent < InodeUnhealthyThreshold)
            {
                unhealthy = true;
                issues.Add($"CRITICAL: Only {Math.Round(freePercent * 100, 1)}% inodes free on data directory mount. New files cannot be created.");
            }
            else if (freePercent < InodeDegradedThreshold)
            {
                degraded = true;
                issues.Add($"Low inode availability: {Math.Round(freePercent * 100, 1)}% free on data directory mount.");
            }
        }

        if (unhealthy)
            return Task.FromResult(HealthCheckResult.Unhealthy(string.Join("; ", issues), data: data));

        if (degraded)
            return Task.FromResult(HealthCheckResult.Degraded(string.Join("; ", issues), data: data));

        return Task.FromResult(HealthCheckResult.Healthy("Linux resource limits are within acceptable thresholds.", data));
    }

    /// <summary>Reads /proc/sys/fs/inotify/max_user_watches. Returns -1 on failure.</summary>
    internal static int ReadInotifyWatchLimit()
    {
        try
        {
            var text = File.ReadAllText("/proc/sys/fs/inotify/max_user_watches").Trim();
            return int.TryParse(text, out var value) ? value : -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Reads inode statistics for the mount point containing <paramref name="path"/>
    /// using <c>statvfs()</c> P/Invoke.
    /// </summary>
    internal static bool TryGetInodeInfo(string path, out long totalInodes, out long freeInodes)
    {
        totalInodes = 0;
        freeInodes = 0;

        if (!OperatingSystem.IsLinux())
            return false;

        try
        {
            if (Statvfs(path, out var stat) != 0)
                return false;

            totalInodes = (long)stat.f_files;
            freeInodes = (long)stat.f_ffree;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("libc", EntryPoint = "statvfs", StringMarshalling = StringMarshalling.Utf8)]
    [SupportedOSPlatform("linux")]
    private static partial int Statvfs(string path, out StatvfsResult buf);

    // Matches struct statvfs on Linux x86-64 (glibc)
    [StructLayout(LayoutKind.Sequential)]
    private struct StatvfsResult
    {
#pragma warning disable CS0649 // Fields set by P/Invoke
        public ulong f_bsize;    // block size
        public ulong f_frsize;   // fragment size
        public ulong f_blocks;   // total blocks
        public ulong f_bfree;    // free blocks
        public ulong f_bavail;   // available blocks
        public ulong f_files;    // total inodes
        public ulong f_ffree;    // free inodes
        public ulong f_favail;   // available inodes
        public ulong f_fsid;     // file system id
        public ulong f_flag;     // mount flags
        public ulong f_namemax;  // max filename length
        private readonly ulong _pad1;
        private readonly ulong _pad2;
        private readonly ulong _pad3;
        private readonly ulong _pad4;
        private readonly ulong _pad5;
        private readonly ulong _pad6;
#pragma warning restore CS0649
    }
}
