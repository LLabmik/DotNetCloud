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
        try
        {
            await _concurrencyGate.WaitAsync(linkedCts.Token);
            semaphoreAcquired = true;

            _activeJobs[job.Id] = job;
            job.Status = TranscodingJobStatus.Running;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty
            };

            using var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

            // Capture stderr for error reporting (can't mix BeginErrorReadLine + ReadToEndAsync)
            var stderrCapture = new System.Text.StringBuilder();

            // Set up progress parsing from stderr
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null)
                    return;
                stderrCapture.AppendLine(e.Data);
                _logger.LogDebug("ffmpeg: {Line}", e.Data);
                ParseProgress(e.Data, job, totalDuration);
            };

            _logger.LogInformation(
                "Starting ffmpeg: {FfmpegPath} {Arguments}",
                _options.FfmpegPath, arguments);

            process.Start();
            job.ProcessId = process.Id;
            process.BeginErrorReadLine();

            // Read stdout to prevent buffer deadlock (ffmpeg may write to stdout)
            _ = ConsumeStdoutAsync(process);

            // Wait for exit or cancellation (from either the caller's token or CancelJob)
            using var ctr = linkedCts.Token.Register(() =>
            {
                _logger.LogInformation("Cancelling ffmpeg job {JobId}", job.Id);
                SendGracefulQuit(process);
            });

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                SendGracefulQuit(process);
                // Give ffmpeg 5 seconds to flush and exit
                if (!process.HasExited)
                {
                    await Task.WhenAny(
                        process.WaitForExitAsync(CancellationToken.None),
                        Task.Delay(5000, CancellationToken.None));
                }

                if (!process.HasExited)
                {
                    _logger.LogWarning("Force-killing ffmpeg job {JobId}", job.Id);
                    process.Kill(entireProcessTree: true);
                }
            }

            // Ensure stderr reading is complete
            process.CancelErrorRead();

            if (process.ExitCode != 0)
            {
                var error = stderrCapture.ToString();
                _logger.LogError(
                    "ffmpeg exited with code {ExitCode} for job {JobId}. Stderr:\n{Stderr}",
                    process.ExitCode, job.Id, error);
                // Also write to stderr so the process supervisor captures it at Error level
                Console.Error.WriteLine($"[VIDEO-FFMPEG-FAIL] exit={process.ExitCode} job={job.Id}");
                Console.Error.WriteLine(error);
                throw new FfmpegException(
                    $"ffmpeg exited with code {process.ExitCode}",
                    process.ExitCode,
                    error);
            }

            _logger.LogInformation("ffmpeg job {JobId} completed successfully", job.Id);
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
    /// Kills a process by PID. Handles all common failure modes silently
    /// (process already exited, PID recycled, permission denied).
    /// </summary>
    private void KillProcessByPid(int pid, string jobId)
    {
        if (pid <= 0)
            return;

        try
        {
            var process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                _logger.LogWarning("Direct-killing ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
                process.Kill(entireProcessTree: true);
                _logger.LogInformation("Direct-killed ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
            }
        }
        catch (ArgumentException)
        {
            // Process already exited (PID no longer exists)
            _logger.LogDebug("ffmpeg process pid={Pid} already exited (job {JobId})", pid, jobId);
        }
        catch (InvalidOperationException)
        {
            // Process already exited (HasExited would be true, or can't access)
            _logger.LogDebug("ffmpeg process pid={Pid} already exited (job {JobId})", pid, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill ffmpeg process pid={Pid} for job {JobId}", pid, jobId);
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
    /// Sends 'q' to ffmpeg's stdin for graceful quit.
    /// ffmpeg finalizes the output file before exiting.
    /// </summary>
    private static void SendGracefulQuit(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("q");
            }
        }
        catch (Exception)
        {
            // Process may have already exited
        }
    }

    /// <summary>
    /// Consumes stdout to prevent buffer deadlocks (ffmpeg may print banner to stdout).
    /// </summary>
    private static async Task ConsumeStdoutAsync(Process process)
    {
        try
        {
            await process.StandardOutput.ReadToEndAsync();
        }
        catch
        {
            // Ignore — process may have exited
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
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
