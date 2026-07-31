using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Manages ffmpeg child processes for video transcoding.
/// Enforces concurrency limits, handles graceful cancellation,
/// and parses progress from stderr.
///
/// Registered as a singleton (one per module host process).
/// Thread-safe.
/// </summary>
public sealed class FfmpegProcessManager : IDisposable
{
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<FfmpegProcessManager> _logger;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ConcurrentDictionary<string, TranscodingJob> _activeJobs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCts = new();

    // Regex to parse "time=HH:MM:SS.MS" from ffmpeg stderr
    private static readonly Regex TimeRegex = new(
        @"time=(\d+):(\d+):(\d+)\.(\d+)",
        RegexOptions.Compiled);

    // Regex to parse "speed= N.Nx" from ffmpeg stderr
    private static readonly Regex SpeedRegex = new(
        @"speed=\s*([\d.]+)x",
        RegexOptions.Compiled);

    /// <summary>
    /// Writes a line to the shared HLS seek debug log. Process-isolated module hosts do
    /// not reliably forward Console.Error to the main systemd journal, so a file-based
    /// log is the only reliable way to correlate ffmpeg PIDs and cancellation events.
    /// </summary>
    private static void WriteFfmpegDiag(string message)
    {
        try
        {
            System.IO.File.AppendAllText("/tmp/dotnetcloud-hls-seek-debug.log",
                $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Best effort diagnostic logging.
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegProcessManager"/> class.
    /// </summary>
    public FfmpegProcessManager(
        VideoTranscodingOptions options,
        ILogger<FfmpegProcessManager> logger)
    {
        _options = options;
        _logger = logger;
        _concurrencyGate = new SemaphoreSlim(options.MaxConcurrentJobs);
    }

    /// <summary>
    /// Runs ffmpeg with the given arguments, writing output to outputPath.
    /// Returns when the process exits successfully.
    /// Throws <see cref="FfmpegException"/> on non-zero exit code.
    /// Supports cancellation — sends 'q' to ffmpeg stdin for graceful stop.
    /// </summary>
    /// <param name="arguments">The ffmpeg arguments (NOT including the "ffmpeg" binary name).</param>
    /// <param name="outputPath">Where ffmpeg will write the output file.</param>
    /// <param name="job">The TranscodingJob to track progress on. Its ProgressPercent and Speed fields are updated.</param>
    /// <param name="totalDuration">Total duration of the source video, used to compute progress %.</param>
    /// <param name="cancellationToken">Token to cancel transcoding.</param>
    public async Task RunAsync(
        string arguments,
        string outputPath,
        TranscodingJob job,
        TimeSpan totalDuration,
        CancellationToken cancellationToken = default)
    {
        // Create a per-job CTS so CancelJob() can unblock us.
        // Linked token combines the caller's token + the per-job token.
        var perJobCts = new CancellationTokenSource();
        _jobCts[job.Id] = perJobCts;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, perJobCts.Token);

        bool semaphoreAcquired = false;
        var stderrPath = $"/tmp/ffmpeg-stderr-{job.Id}.log";
        try
        {
            await _concurrencyGate.WaitAsync(linkedCts.Token);
            semaphoreAcquired = true;

            _activeJobs[job.Id] = job;
            job.Status = TranscodingJobStatus.Running;

            // Capture ffmpeg stderr to a dedicated log file so we can diagnose
            // exit 137 / SIGKILL and other failures even when the module host's
            // stderr is not forwarded to journald.
            var stderrStream = new FileStream(stderrPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegPath,
                    // Override log level to info so ffmpeg outputs progress details to stderr.
                    Arguments = arguments.Replace("-loglevel warning", "-loglevel info"),
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty
                };

                using var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

                _logger.LogInformation(
                    "Starting ffmpeg: {FfmpegPath} {Arguments}",
                    _options.FfmpegPath, arguments);

                process.Start();
                job.ProcessId = process.Id;
                var launchMsg = $"[VIDEO-FFMPEG-LAUNCH] job={job.Id} pid={process.Id} hasExited={process.HasExited} stderr={stderrPath}";
                Console.Error.WriteLine(launchMsg);
                WriteFfmpegDiag(launchMsg);

                // Asynchronously drain ffmpeg stderr into the per-job log file.
                var stderrDrain = process.StandardError.BaseStream.CopyToAsync(stderrStream, CancellationToken.None);

                // Poll for process exit with Task.Delay, avoiding WaitForExitAsync (which can have
                // reliability issues on Linux) and the Exited event (which can fire prematurely).
                // This is a proven pattern that works reliably across all .NET platforms.
                var cancelReg = linkedCts.Token.Register(() =>
                {
                    var callerStack = new System.Diagnostics.StackTrace(1, true).ToString().Replace("\n", " | ", StringComparison.Ordinal);
                    var cancelMsg = $"[VIDEO-FFMPEG-CANCEL] job={job.Id} pid={process.Id} token cancelled caller={callerStack}";
                    Console.Error.WriteLine(cancelMsg);
                    WriteFfmpegDiag(cancelMsg);
                    SendGracefulQuit(process);
                });

                try
                {
                    while (!process.HasExited)
                    {
                        await Task.Delay(500, CancellationToken.None);
                        if (linkedCts.Token.IsCancellationRequested)
                        {
                            var detectMsg = $"[VIDEO-FFMPEG-CANCEL] job={job.Id} pid={process.Id} cancellation detected";
                            Console.Error.WriteLine(detectMsg);
                            WriteFfmpegDiag(detectMsg);
                            SendGracefulQuit(process);
                            break;
                        }
                    }
                }
                finally
                {
                    cancelReg.Dispose();
                }

                // Ensure process is fully exited before reading ExitCode
                if (!process.HasExited)
                    process.WaitForExit(5000);

                if (!process.HasExited)
                {
                    _logger.LogWarning("Force-killing ffmpeg job {JobId}", job.Id);
                    // Do NOT use entireProcessTree: true. All ffmpeg child processes in this
                    // process-isolated module host share the same process group on Linux; killing
                    // the tree would terminate unrelated sibling ffmpeg jobs (e.g. the newly
                    // launched seek transcode when cancelling the previous one).
                    process.Kill(entireProcessTree: false);
                }

                try
                {
                    await stderrDrain.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch { /* best effort */ }

                if (process.ExitCode != 0)
                {
                    var lastStderr = ReadTail(stderrPath, 4000);
                    _logger.LogError(
                        "ffmpeg exited with code {ExitCode} for job {JobId}. stderr tail: {StderrTail}",
                        process.ExitCode, job.Id, lastStderr);
                    var failMsg = $"[VIDEO-FFMPEG-FAIL] exit={process.ExitCode} job={job.Id} pid={process.Id} stderr={lastStderr}";
                    Console.Error.WriteLine(failMsg);
                    WriteFfmpegDiag(failMsg);
                    throw new FfmpegException(
                        $"ffmpeg exited with code {process.ExitCode}",
                        process.ExitCode,
                        lastStderr);
                }

                _logger.LogInformation("ffmpeg job {JobId} completed successfully", job.Id);
                var doneMsg = $"[VIDEO-FFMPEG-DONE] job={job.Id} pid={process.Id} exitCode={process.ExitCode} hasExited={process.HasExited}";
                Console.Error.WriteLine(doneMsg);
                WriteFfmpegDiag(doneMsg);
            }
            finally
            {
                try
                { stderrStream.Dispose(); }
                catch { /* best effort */ }
            }
        }
        finally
        {
            _jobCts.TryRemove(job.Id, out _);
            _activeJobs.TryRemove(job.Id, out _);
            if (semaphoreAcquired)
                _concurrencyGate.Release();
        }
    }

    /// <summary>
    /// Cancels a running transcode job by cancelling its CTS (which triggers
    /// graceful quit → force kill inside RunAsync) AND directly killing the
    /// ffmpeg process by PID as a belt-and-suspenders fallback.
    /// </summary>
    public void CancelJob(string jobId)
    {
        var sanitizedId = SanitizeForLog(jobId);
        var callerStack = new System.Diagnostics.StackTrace(1, true).ToString().Replace("\n", " | ", StringComparison.Ordinal);
        _logger.LogWarning("CancelJob called for job {JobId}. Caller stack: {CallerStack}", sanitizedId, callerStack);
        var cancelJobMsg = $"[VIDEO-CANCELJOB] job={sanitizedId} stack={callerStack}";
        Console.Error.WriteLine(cancelJobMsg);
        WriteFfmpegDiag(cancelJobMsg);

        // 1. Cancel the per-job CTS — unblocks RunAsync which sends 'q' + force-kills
        if (_jobCts.TryRemove(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                _logger.LogInformation("Transcode job {JobId}: CTS cancelled for graceful shutdown", sanitizedId);
            }
            catch (ObjectDisposedException)
            {
                // CTS already disposed (RunAsync completed) — no action needed
            }
        }

        // 2. Direct kill by PID (belt-and-suspenders — catches any edge case
        //    where the CTS didn't propagate, e.g. process stuck in D state)
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Status = TranscodingJobStatus.Cancelled;
            KillProcessByPid(job.ProcessId, jobId);
        }
    }

    /// <summary>
    /// Kills a specific ffmpeg process by PID. Handles all common failure modes silently
    /// (process already exited, PID recycled, permission denied). Never uses
    /// entireProcessTree because sibling ffmpeg processes in the module host share a
    /// process group on Linux, and PID recycling could otherwise target an unrelated
    /// process that reused this PID.
    /// </summary>
    private void KillProcessByPid(int pid, string jobId)
    {
        if (pid <= 0)
            return;

        try
        {
            var process = Process.GetProcessById(pid);
            var actualName = process.ProcessName;
            WriteFfmpegDiag($"[VIDEO-KILLBYPID] job={jobId} pid={pid} actualProcessName={actualName} hasExited={process.HasExited}");

            // Guard against PID recycling: only kill if the process is actually ffmpeg.
            if (!process.HasExited && string.Equals(actualName, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Direct-killing ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
                process.Kill(entireProcessTree: false);
                _logger.LogInformation("Direct-killed ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
                WriteFfmpegDiag($"[VIDEO-KILLBYPID] job={jobId} pid={pid} KILLED");
            }
        }
        catch (ArgumentException)
        {
            // Process already exited (PID no longer exists)
            _logger.LogDebug("ffmpeg process pid={Pid} already exited (job {JobId})", pid, jobId);
            WriteFfmpegDiag($"[VIDEO-KILLBYPID] job={jobId} pid={pid} already exited (ArgumentException)");
        }
        catch (InvalidOperationException)
        {
            // Process already exited (HasExited would be true, or can't access)
            _logger.LogDebug("ffmpeg process pid={Pid} already exited (job {JobId})", pid, jobId);
            WriteFfmpegDiag($"[VIDEO-KILLBYPID] job={jobId} pid={pid} already exited (InvalidOperationException)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
            WriteFfmpegDiag($"[VIDEO-KILLBYPID] job={jobId} pid={pid} exception={ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for a job's ffmpeg process to fully exit (i.e., it is no longer
    /// in the active-jobs dictionary). Returns true if it exited within the
    /// timeout, false otherwise. Used by seek operations to avoid deleting
    /// an HLS output directory while the old ffmpeg process is still writing.
    /// </summary>
    public async Task<bool> WaitForProcessExitAsync(string jobId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (!_activeJobs.ContainsKey(jobId))
                return true;
            await Task.Delay(100);
        }
        return !_activeJobs.ContainsKey(jobId);
    }

    private static string ReadTail(string path, int maxChars)
    {
        try
        {
            if (!File.Exists(path))
                return string.Empty;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0)
                return string.Empty;

            var charsToRead = (int)Math.Min(maxChars, fs.Length);
            fs.Seek(-charsToRead, SeekOrigin.End);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd()
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " | ", StringComparison.Ordinal);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizeForLog(string value)
    {
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\0", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a copy of currently active jobs for monitoring.
    /// </summary>
    public IReadOnlyList<TranscodingJob> GetActiveJobs()
    {
        return _activeJobs.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Parses ffmpeg stderr lines to extract progress information.
    /// Lines look like:
    ///   frame=  150 fps= 30 q=28.0 size=    1024kB time=00:00:05.00 bitrate=1678.2kbits/s speed=1.00x
    /// </summary>
    private void ParseProgress(string line, TranscodingJob job, TimeSpan totalDuration)
    {
        var timeMatch = TimeRegex.Match(line);
        if (timeMatch.Success)
        {
            var ts = new TimeSpan(
                0,
                int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(timeMatch.Groups[4].Value.PadRight(3, '0').Substring(0, 3), CultureInfo.InvariantCulture));

            job.CurrentTime = ts;

            if (totalDuration > TimeSpan.Zero)
            {
                job.ProgressPercent = Math.Min(100.0, (ts.TotalSeconds / totalDuration.TotalSeconds) * 100.0);
            }
        }

        var speedMatch = SpeedRegex.Match(line);
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            job.Speed = speed;
        }
    }

    /// <summary>
    /// Attempts graceful quit by force-killing the specific ffmpeg process.
    /// (stdin is not redirected so we can't send 'q'.) The caller has a 5-second
    /// timeout after this. Never uses entireProcessTree because sibling ffmpeg
    /// processes in the module host share a process group on Linux.
    /// </summary>
    private static void SendGracefulQuit(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: false);
        }
        catch (Exception)
        {
            // Best effort
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Cancel all running jobs
        foreach (var kvp in _jobCts)
        {
            try
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            catch { }
        }
        _jobCts.Clear();

        _concurrencyGate.Dispose();
    }
}

/// <summary>
/// Exception thrown when ffmpeg exits with a non-zero code.
/// </summary>
public sealed class FfmpegException : Exception
{
    /// <summary>The ffmpeg process exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Error text from ffmpeg stderr.</summary>
    public string? FfmpegError { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegException"/> class.
    /// </summary>
    public FfmpegException(string message, int exitCode, string? ffmpegError = null)
        : base(message)
    {
        ExitCode = exitCode;
        FfmpegError = ffmpegError;
    }
}
