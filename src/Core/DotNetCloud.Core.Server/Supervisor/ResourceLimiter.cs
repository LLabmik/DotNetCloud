using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DotNetCloud.Core;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Applies resource limits to module processes using platform-specific mechanisms.
/// - Linux: cgroups v2 (requires root or appropriate cgroup delegation)
/// - Windows: Job Objects via Win32 P/Invoke
/// </summary>
internal sealed partial class ResourceLimiter : IDisposable
{
    private readonly ILogger<ResourceLimiter> _logger;
    private readonly Dictionary<int, nint> _jobHandles = new(); // PID → Job Object handle (Windows)
    private readonly string _cgroupBasePath;
    private bool _disposed;

    /// <summary>
    /// Fallback base path for cgroup v2 filesystem when detection fails.
    /// </summary>
    private const string DefaultCgroupBase = "/sys/fs/cgroup/dotnetcloud";

    public ResourceLimiter(ILogger<ResourceLimiter> logger)
    {
        _logger = logger;
        _cgroupBasePath = ResolveCgroupBasePath();
    }

    /// <summary>
    /// Resolves the cgroup base path for this process by reading /proc/self/cgroup.
    /// With systemd cgroup delegation, the service's cgroup subtree is writable.
    /// Falls back to <see cref="DefaultCgroupBase"/> if detection fails.
    /// </summary>
    private static string ResolveCgroupBasePath()
    {
        try
        {
            // /proc/self/cgroup format with cgroups v2: "0::/system.slice/dotnetcloud.service\n"
            var cgroupLine = File.ReadAllText("/proc/self/cgroup").Trim();
            // cgroups v2: single line, colon-separated: "0::/path"
            var parts = cgroupLine.Split(':', 3);
            if (parts.Length == 3 && !string.IsNullOrEmpty(parts[2]))
            {
                var controllerPath = parts[2].TrimEnd('\n', '\r');
                return "/sys/fs/cgroup" + controllerPath;
            }
        }
        catch (Exception)
        {
            // Detection failed — fall back to default
        }
        return DefaultCgroupBase;
    }

    /// <summary>
    /// Applies memory and CPU limits to a module process.
    /// </summary>
    /// <param name="moduleId">The module identifier (used for cgroup/job naming).</param>
    /// <param name="process">The process to limit.</param>
    /// <param name="memoryLimitBytes">Maximum memory in bytes, or null for no limit.</param>
    /// <param name="cpuPercent">CPU limit as percentage (0-100), or null for no limit.</param>
    /// <returns>True if limits were applied; false if unsupported or failed.</returns>
    public bool ApplyLimits(string moduleId, Process process, long? memoryLimitBytes, int? cpuPercent)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        if (process is null || process.HasExited)
        {
            _logger.LogWarning("Cannot apply limits to null or exited process for module {ModuleId}", SanitizeForLog(moduleId));
            return false;
        }

        if (!memoryLimitBytes.HasValue && !cpuPercent.HasValue)
        {
            _logger.LogDebug("No resource limits configured for module {ModuleId}", SanitizeForLog(moduleId));
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ApplyLinuxCgroupLimits(moduleId, process, memoryLimitBytes, cpuPercent);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ApplyWindowsJobObjectLimits(moduleId, process, memoryLimitBytes, cpuPercent);
        }

        _logger.LogWarning("Resource limits not supported on platform {OS}", RuntimeInformation.OSDescription);
        return false;
    }

    /// <summary>
    /// Removes resource limits and cleans up for a module process.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="processId">The process ID that was limited.</param>
    public void RemoveLimits(string moduleId, int processId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            RemoveLinuxCgroup(moduleId);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RemoveWindowsJobObject(processId);
        }
    }

    // ──────────────────── Linux cgroups v2 ────────────────────

    [SupportedOSPlatform("linux")]
    private bool ApplyLinuxCgroupLimits(string moduleId, Process process, long? memoryLimitBytes, int? cpuPercent)
    {
        var cgroupPath = GetCgroupPath(moduleId);
        var parentPath = Path.GetDirectoryName(cgroupPath)!;

        try
        {
            // Enable controllers in the parent's subtree_control so child cgroups get cpu.max / memory.max
            EnableSubtreeControllers(parentPath);

            // Create cgroup directory for this module
            if (!Directory.Exists(cgroupPath))
            {
                Directory.CreateDirectory(cgroupPath);
                _logger.LogDebug("Created cgroup directory {Path}", SanitizeForLog(cgroupPath));
            }

            // Apply memory limit: write to memory.max
            if (memoryLimitBytes.HasValue)
            {
                var memoryMaxPath = Path.Combine(cgroupPath, "memory.max");
                File.WriteAllText(memoryMaxPath, memoryLimitBytes.Value.ToString());
                _logger.LogInformation(
                    "Set memory limit for module {ModuleId} (PID {ProcessId}): {MemoryMb}MB",
                    SanitizeForLog(moduleId), process.Id, memoryLimitBytes.Value / 1024 / 1024);

                // Also set memory.swap.max to 0 to prevent swap usage
                var swapMaxPath = Path.Combine(cgroupPath, "memory.swap.max");
                if (File.Exists(swapMaxPath) || File.Exists(Path.Combine(cgroupPath, "memory.swap.current")))
                {
                    try
                    {
                        File.WriteAllText(swapMaxPath, "0");
                    }
                    catch (IOException)
                    {
                        _logger.LogDebug("Swap limit not available for cgroup {ModuleId}", SanitizeForLog(moduleId));
                    }
                }
            }

            // Apply CPU limit: write to cpu.max
            // cpu.max format: "$MAX $PERIOD" (e.g., "50000 100000" = 50% of one CPU)
            if (cpuPercent.HasValue)
            {
                var cpuMaxPath = Path.Combine(cgroupPath, "cpu.max");
                var period = 100_000; // 100ms period (default)
                var quota = cpuPercent.Value * period / 100;
                File.WriteAllText(cpuMaxPath, $"{quota} {period}");
                _logger.LogInformation(
                    "Set CPU limit for module {ModuleId} (PID {ProcessId}): {CpuPercent}",
                    SanitizeForLog(moduleId), process.Id, cpuPercent.Value);
            }

            // Assign process to cgroup by writing PID to cgroup.procs
            var procsPath = Path.Combine(cgroupPath, "cgroup.procs");
            File.WriteAllText(procsPath, process.Id.ToString());
            _logger.LogInformation(
                "Assigned PID {ProcessId} to cgroup {CgroupPath}",
                process.Id, SanitizeForLog(cgroupPath));

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Cannot apply cgroup limits for module {ModuleId} — " +
                "controller files not available (cgroup.subtree_control may be busy). " +
                "Limits will not be enforced for this module. Path: {CgroupPath}",
                SanitizeForLog(moduleId), SanitizeForLog(cgroupPath));
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex,
                "Cannot apply cgroup limits for module {ModuleId} — " +
                "I/O error accessing cgroup filesystem. Path: {CgroupPath}",
                SanitizeForLog(moduleId), SanitizeForLog(cgroupPath));
            return false;
        }
    }

    [SupportedOSPlatform("linux")]
    private void RemoveLinuxCgroup(string moduleId)
    {
        var cgroupPath = GetCgroupPath(moduleId);

        try
        {
            if (Directory.Exists(cgroupPath))
            {
                // cgroup directories can only be removed when empty (no processes)
                Directory.Delete(cgroupPath, recursive: false);
                _logger.LogDebug("Removed cgroup directory {Path}", SanitizeForLog(cgroupPath));
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not remove cgroup directory {Path} (may still have processes)", SanitizeForLog(cgroupPath));
        }
    }

    private string GetCgroupPath(string moduleId)
    {
        // Sanitize module ID for use in filesystem path
        var safeName = moduleId.Replace('.', '-');
        return Path.Combine(_cgroupBasePath, safeName);
    }

    /// <summary>
    /// Enables cpu and memory controllers in the cgroup subtree_control chain
    /// from the service root down to the immediate parent of the module cgroup.
    /// Without this, child cgroups won't have cpu.max / memory.max files.
    /// </summary>
    private static void EnableSubtreeControllers(string moduleParentPath)
    {
        try
        {
            // Collect all ancestor cgroup directories that need subtree_control enabled.
            // Walk from the module parent up to (but not including) /sys/fs/cgroup.
            var ancestors = new List<string>();
            var current = moduleParentPath;
            var cgroupRoot = "/sys/fs/cgroup".TrimEnd('/');

            while (!string.IsNullOrEmpty(current) && current != cgroupRoot && current != "/")
            {
                var subtreePath = Path.Combine(current, "cgroup.subtree_control");
                if (File.Exists(subtreePath))
                    ancestors.Add(current);
                current = Path.GetDirectoryName(current) ?? "";
            }

            // Enable controllers from root to leaf (top-down)
            ancestors.Reverse();
            foreach (var ancestor in ancestors)
            {
                var subtreePath = Path.Combine(ancestor, "cgroup.subtree_control");
                var enabled = File.ReadAllText(subtreePath).Trim();
                var toAdd = new List<string>(2);

                if (!enabled.Contains("cpu"))
                    toAdd.Add("cpu");
                if (!enabled.Contains("memory"))
                    toAdd.Add("memory");

                if (toAdd.Count > 0)
                {
                    File.WriteAllText(subtreePath, $"+{string.Join(" +", toAdd)}");
                }
            }
        }
        catch (Exception)
        {
            // Controller enablement is best-effort; limits will fail gracefully later
        }
    }

    // ──────────────────── Windows Job Objects ────────────────────

    [SupportedOSPlatform("windows")]
    private bool ApplyWindowsJobObjectLimits(string moduleId, Process process, long? memoryLimitBytes, int? cpuPercent)
    {
        try
        {
            // Create a Job Object for this module
            var jobName = $"DotNetCloud_Module_{moduleId.Replace('.', '_')}";
            var jobHandle = NativeMethods.CreateJobObject(nint.Zero, jobName);

            if (jobHandle == nint.Zero)
            {
                var error = Marshal.GetLastPInvokeError();
                _logger.LogError("CreateJobObject failed for module {ModuleId}: Win32 error {Error}", SanitizeForLog(moduleId), error);
                return false;
            }

            // Configure limits
            var extendedLimit = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();

            if (memoryLimitBytes.HasValue)
            {
                extendedLimit.BasicLimitInformation.LimitFlags |= NativeMethods.JOB_OBJECT_LIMIT_PROCESS_MEMORY;
                extendedLimit.ProcessMemoryLimit = (nuint)memoryLimitBytes.Value;

                _logger.LogInformation(
                    "Set Job Object memory limit for module {ModuleId} (PID {ProcessId}): {MemoryMb}MB",
                    SanitizeForLog(moduleId), process.Id, memoryLimitBytes.Value / 1024 / 1024);
            }

            // CPU rate control (Windows 8+)
            if (cpuPercent.HasValue)
            {
                var cpuRate = new NativeMethods.JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
                {
                    ControlFlags = NativeMethods.JOB_OBJECT_CPU_RATE_CONTROL_ENABLE |
                                   NativeMethods.JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP,
                    CpuRate = (uint)(cpuPercent.Value * 100) // Value is in 1/100ths of a percent
                };

                var cpuRateSize = Marshal.SizeOf<NativeMethods.JOBOBJECT_CPU_RATE_CONTROL_INFORMATION>();
                var cpuRatePtr = Marshal.AllocHGlobal(cpuRateSize);
                try
                {
                    Marshal.StructureToPtr(cpuRate, cpuRatePtr, false);
                    var cpuResult = NativeMethods.SetInformationJobObject(
                        jobHandle,
                        NativeMethods.JOBOBJECTINFOCLASS.JobObjectCpuRateControlInformation,
                        cpuRatePtr,
                        (uint)cpuRateSize);

                    if (!cpuResult)
                    {
                        _logger.LogWarning(
                            "Failed to set CPU rate limit for module {ModuleId}: Win32 error {Error}",
                        SanitizeForLog(moduleId), Marshal.GetLastPInvokeError());
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Set Job Object CPU limit for module {ModuleId} (PID {ProcessId}): {CpuPercent}%",
                            SanitizeForLog(moduleId), process.Id, cpuPercent.Value);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(cpuRatePtr);
                }
            }

            // Apply memory limit
            if (memoryLimitBytes.HasValue)
            {
                var limitSize = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                var limitPtr = Marshal.AllocHGlobal(limitSize);
                try
                {
                    Marshal.StructureToPtr(extendedLimit, limitPtr, false);
                    var memResult = NativeMethods.SetInformationJobObject(
                        jobHandle,
                        NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                        limitPtr,
                        (uint)limitSize);

                    if (!memResult)
                    {
                        _logger.LogWarning(
                            "Failed to set memory limit for module {ModuleId}: Win32 error {Error}",
                            moduleId, Marshal.GetLastPInvokeError());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(limitPtr);
                }
            }

            // Assign process to job object
            if (!NativeMethods.AssignProcessToJobObject(jobHandle, process.Handle))
            {
                var error = Marshal.GetLastPInvokeError();
                _logger.LogError(
                    "AssignProcessToJobObject failed for module {ModuleId} (PID {ProcessId}): Win32 error {Error}",
                    SanitizeForLog(moduleId), process.Id, error);
                NativeMethods.CloseHandle(jobHandle);
                return false;
            }

            // Track the job handle for cleanup
            _jobHandles[process.Id] = jobHandle;

            _logger.LogInformation(
                "Assigned PID {ProcessId} to Job Object for module {ModuleId}",
                process.Id, SanitizeForLog(moduleId));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Windows Job Object limits for module {ModuleId}", SanitizeForLog(moduleId));
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private void RemoveWindowsJobObject(int processId)
    {
        if (_jobHandles.TryGetValue(processId, out var jobHandle))
        {
            NativeMethods.CloseHandle(jobHandle);
            _jobHandles.Remove(processId);
            _logger.LogDebug("Closed Job Object handle for PID {ProcessId}", processId);
        }
    }

    private static string SanitizeForLog(string? value)
    {
        return LogSanitizer.Sanitize(value ?? string.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Clean up all Windows Job Object handles
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var kvp in _jobHandles)
            {
                NativeMethods.CloseHandle(kvp.Value);
            }
            _jobHandles.Clear();
        }

        // Clean up Linux cgroup directories
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Directory.Exists(_cgroupBasePath))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(_cgroupBasePath))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: false);
                    }
                    catch (IOException)
                    {
                        // May still have processes
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up cgroup directories");
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Win32 P/Invoke declarations for Job Object management.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static partial class NativeMethods
    {
        public const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
        public const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
        public const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;

        public enum JOBOBJECTINFOCLASS
        {
            JobObjectExtendedLimitInformation = 9,
            JobObjectCpuRateControlInformation = 15
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            public uint ControlFlags;
            public uint CpuRate;
        }

        [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial nint CreateJobObject(nint lpJobAttributes, string? lpName);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetInformationJobObject(nint hJob, JOBOBJECTINFOCLASS jobObjectInfoClass, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AssignProcessToJobObject(nint hJob, nint hProcess);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(nint hObject);
    }
}
