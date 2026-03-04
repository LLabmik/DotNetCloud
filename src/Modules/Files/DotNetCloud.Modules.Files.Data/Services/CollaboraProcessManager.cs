using System.Diagnostics;
using System.Runtime.InteropServices;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Background service that manages the lifecycle of a locally-installed Collabora Online (CODE) process.
/// Only active when <c>CollaboraOptions.UseBuiltInCollabora</c> is <c>true</c>.
/// </summary>
/// <remarks>
/// The manager:
/// <list type="bullet">
/// <item>Locates the Collabora executable from <c>CollaboraOptions.CollaboraExecutablePath</c>
///       or discovers it within <c>CollaboraOptions.CollaboraInstallDirectory</c>.</item>
/// <item>Starts the process and monitors health via <see cref="ICollaboraDiscoveryService"/>.</item>
/// <item>Restarts the process on crash with exponential backoff up to
///       <c>CollaboraOptions.CollaboraMaxRestartAttempts</c> attempts.</item>
/// <item>Stops the process on application shutdown.</item>
/// </list>
/// </remarks>
internal sealed class CollaboraProcessManager : BackgroundService, ICollaboraProcessManager
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartupWaitTimeout = TimeSpan.FromSeconds(60);

    private readonly CollaboraOptions _options;
    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly ILogger<CollaboraProcessManager> _logger;

    private Process? _process;
    private int _restartCount;
    private CollaboraProcessStatus _status = CollaboraProcessStatus.NotConfigured;
    private readonly object _lock = new();

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _process is not null && !_process.HasExited;
            }
        }
    }

    /// <inheritdoc />
    public CollaboraProcessStatus Status
    {
        get { lock (_lock) { return _status; } }
        private set { lock (_lock) { _status = value; } }
    }

    /// <inheritdoc />
    public int RestartCount => _restartCount;

    /// <summary>
    /// Initializes a new instance of <see cref="CollaboraProcessManager"/>.
    /// </summary>
    public CollaboraProcessManager(
        IOptions<CollaboraOptions> options,
        ICollaboraDiscoveryService discoveryService,
        ILogger<CollaboraProcessManager> logger)
    {
        _options = options.Value;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.UseBuiltInCollabora || !_options.Enabled)
        {
            _logger.LogDebug("Collabora process manager is inactive (UseBuiltInCollabora=false or Enabled=false).");
            Status = CollaboraProcessStatus.NotConfigured;
            return;
        }

        var executablePath = ResolveExecutablePath();
        if (executablePath is null)
        {
            _logger.LogError(
                "Collabora executable not found. Set CollaboraExecutablePath or CollaboraInstallDirectory in config.");
            Status = CollaboraProcessStatus.Failed;
            return;
        }

        _logger.LogInformation("Collabora process manager starting. Executable: {Path}", executablePath);

        // Yield so the rest of the host can finish starting before we try to launch
        await Task.Yield();

        await RunWithRestartAsync(executablePath, stoppingToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collabora process manager stopping.");
        StopProcess();
        await base.StopAsync(cancellationToken);
    }

    private async Task RunWithRestartAsync(string executablePath, CancellationToken stoppingToken)
    {
        var maxAttempts = _options.CollaboraMaxRestartAttempts;
        var baseBackoff = TimeSpan.FromSeconds(Math.Max(1, _options.CollaboraRestartBackoffSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (maxAttempts > 0 && _restartCount >= maxAttempts)
            {
                _logger.LogError(
                    "Collabora process has failed {Count} times (max {Max}). Giving up.",
                    _restartCount, maxAttempts);
                Status = CollaboraProcessStatus.Failed;
                return;
            }

            Status = CollaboraProcessStatus.Starting;
            var process = StartProcess(executablePath);

            if (process is null)
            {
                Status = CollaboraProcessStatus.Failed;
                return;
            }

            lock (_lock) { _process = process; }

            // Wait for Collabora to become healthy
            var startedHealthy = await WaitForHealthyAsync(stoppingToken);
            if (!startedHealthy && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Collabora did not become healthy within startup timeout.");
                Status = CollaboraProcessStatus.Degraded;
            }
            else if (startedHealthy)
            {
                Status = CollaboraProcessStatus.Running;
                _logger.LogInformation("Collabora is running (PID {Pid}).", process.Id);
            }

            // Monitor the process
            await MonitorProcessAsync(process, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            // Process exited unexpectedly
            _restartCount++;
            Status = CollaboraProcessStatus.Crashed;
            var backoff = baseBackoff * Math.Pow(2, Math.Min(_restartCount - 1, 5)); // max ~5 min
            _logger.LogWarning(
                "Collabora process exited (attempt {Count}/{Max}). Restarting in {Delay:F0}s.",
                _restartCount, maxAttempts > 0 ? maxAttempts.ToString() : "unlimited",
                backoff.TotalSeconds);

            try
            {
                await Task.Delay(backoff, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        StopProcess();
        Status = CollaboraProcessStatus.Stopped;
    }

    private Process? StartProcess(string executablePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? "."
            };

            // Pass the server URL as the WOPI host
            if (!string.IsNullOrWhiteSpace(_options.WopiBaseUrl))
            {
                startInfo.ArgumentList.Add($"--o:net.wopi_enabled=true");
                startInfo.ArgumentList.Add($"--o:net.post_allow.host[0]={ExtractHostname(_options.WopiBaseUrl)}");
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogError("Process.Start returned null for Collabora executable {Path}", executablePath);
                return null;
            }

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogDebug("[collabora] {Line}", args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    _logger.LogWarning("[collabora] STDERR: {Line}", args.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Collabora process started (PID {Pid}).", process.Id);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Collabora process at {Path}.", executablePath);
            return null;
        }
    }

    private async Task<bool> WaitForHealthyAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(StartupWaitTimeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                if (await _discoveryService.IsAvailableAsync(cts.Token))
                    return true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // ignore transient errors during startup
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
    }

    private async Task MonitorProcessAsync(Process process, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !process.HasExited)
        {
            try
            {
                await Task.Delay(HealthPollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!process.HasExited)
            {
                var healthy = await _discoveryService.IsAvailableAsync(stoppingToken);
                Status = healthy ? CollaboraProcessStatus.Running : CollaboraProcessStatus.Degraded;

                if (!healthy)
                    _logger.LogWarning("Collabora health check failed (process still running).");
            }
        }
    }

    private void StopProcess()
    {
        Process? process;
        lock (_lock)
        {
            process = _process;
            _process = null;
        }

        if (process is null || process.HasExited)
            return;

        try
        {
            _logger.LogInformation("Sending termination signal to Collabora (PID {Pid}).", process.Id);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.Kill(entireProcessTree: true);
            }
            else
            {
                // Send SIGTERM for graceful shutdown
                process.Kill();
            }

            if (!process.WaitForExit(5000))
            {
                _logger.LogWarning("Collabora did not exit within 5 seconds after SIGTERM; force killing.");
                process.Kill();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            _logger.LogDebug(ex, "Exception while stopping Collabora process.");
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// Resolves the full path to the Collabora executable (coolwsd or coolwsd.exe).
    /// </summary>
    private string? ResolveExecutablePath()
    {
        // Explicitly configured path takes priority
        if (!string.IsNullOrWhiteSpace(_options.CollaboraExecutablePath) &&
            File.Exists(_options.CollaboraExecutablePath))
        {
            return _options.CollaboraExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(_options.CollaboraInstallDirectory))
            return null;

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                Path.Combine(_options.CollaboraInstallDirectory, "coolwsd.exe"),
                Path.Combine(_options.CollaboraInstallDirectory, "bin", "coolwsd.exe")
            }
            : new[]
            {
                Path.Combine(_options.CollaboraInstallDirectory, "bin", "coolwsd"),
                Path.Combine(_options.CollaboraInstallDirectory, "coolwsd"),
                "/usr/bin/coolwsd",
                "/usr/local/bin/coolwsd"
            };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ExtractHostname(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host;
        return url;
    }
}
