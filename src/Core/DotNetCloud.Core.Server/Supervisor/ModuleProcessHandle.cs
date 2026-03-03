using System.Diagnostics;
using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Represents a running module process with its handle, metadata, and health status.
/// </summary>
internal sealed class ModuleProcessHandle : IDisposable
{
    private readonly object _lock = new();
    private Process? _process;
    private bool _disposed;

    public required string ModuleId { get; init; }
    public required string ModuleName { get; init; }
    public required string Version { get; init; }
    public required string ExecutablePath { get; init; }
    public required string GrpcEndpoint { get; init; }
    public required RestartPolicy RestartPolicy { get; init; }
    public int MaxRestartAttempts { get; init; } = 5;

    public ModuleProcessStatus Status { get; private set; } = ModuleProcessStatus.Stopped;
    public DateTime? StartedAt { get; private set; }
    public DateTime? LastHealthCheckAt { get; private set; }
    public int ConsecutiveRestarts { get; private set; }
    public long TotalRestarts { get; private set; }
    public string? LastError { get; private set; }

    public int? ProcessId
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    return _process?.HasExited == false ? _process.Id : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public long? MemoryUsageBytes
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    return _process?.HasExited == false ? _process.WorkingSet64 : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                try
                {
                    return _process is not null && !_process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public void SetProcess(Process process)
    {
        lock (_lock)
        {
            _process?.Dispose();
            _process = process;
            Status = ModuleProcessStatus.Running;
            StartedAt = DateTime.UtcNow;
        }
    }

    public void SetStatus(ModuleProcessStatus status, string? error = null)
    {
        lock (_lock)
        {
            Status = status;
            if (error is not null)
            {
                LastError = error;
            }

            if (status == ModuleProcessStatus.Stopped)
            {
                StartedAt = null;
            }
        }
    }

    public void RecordHealthCheck()
    {
        lock (_lock)
        {
            LastHealthCheckAt = DateTime.UtcNow;

            if (Status == ModuleProcessStatus.Degraded)
            {
                Status = ModuleProcessStatus.Running;
            }
        }
    }

    public void IncrementRestartCount()
    {
        lock (_lock)
        {
            ConsecutiveRestarts++;
            TotalRestarts++;
        }
    }

    public void ResetRestartCount()
    {
        lock (_lock)
        {
            ConsecutiveRestarts = 0;
        }
    }

    public ModuleProcessInfo ToProcessInfo()
    {
        lock (_lock)
        {
            return new ModuleProcessInfo
            {
                ModuleId = ModuleId,
                ModuleName = ModuleName,
                Version = Version,
                Status = Status,
                ProcessId = ProcessId,
                GrpcEndpoint = GrpcEndpoint,
                RestartPolicy = RestartPolicy,
                StartedAt = StartedAt,
                LastHealthCheckAt = LastHealthCheckAt,
                ConsecutiveRestarts = ConsecutiveRestarts,
                MaxRestartAttempts = MaxRestartAttempts,
                TotalRestarts = TotalRestarts,
                MemoryUsageBytes = MemoryUsageBytes,
                LastError = LastError
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            try
            {
                _process?.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore
            }

            _process?.Dispose();
            _process = null;
            _disposed = true;
        }
    }
}
