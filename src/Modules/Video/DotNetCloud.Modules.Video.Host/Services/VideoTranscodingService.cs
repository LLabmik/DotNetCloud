using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Orchestrates video transcoding: probe → check cache → build args → run ffmpeg → cache result.
/// Registered as scoped service.
/// </summary>
public sealed class VideoTranscodingService : IVideoTranscodingService
{
    private readonly FfmpegArgumentBuilder _argBuilder;
    private readonly FfmpegProcessManager _processManager;
    private readonly TranscodeCacheService _cacheService;
    private readonly TranscodingJobTracker _jobTracker;
    private readonly VideoTranscodingOptions _options;
    private readonly ILogger<VideoTranscodingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoTranscodingService"/> class.
    /// </summary>
    public VideoTranscodingService(
        FfmpegArgumentBuilder argBuilder,
        FfmpegProcessManager processManager,
        TranscodeCacheService cacheService,
        TranscodingJobTracker jobTracker,
        VideoTranscodingOptions options,
        ILogger<VideoTranscodingService> logger)
    {
        _argBuilder = argBuilder;
        _processManager = processManager;
        _cacheService = cacheService;
        _jobTracker = jobTracker;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(StreamingStrategy Strategy, string? VideoCodec, string? AudioCodec, string? Container)> DecideStreamingStrategyAsync(
        string videoFilePath,
        string mimeType,
        CancellationToken ct = default)
    {
        // Run ffprobe to get codec info
        var probeJson = await RunFfprobeAsync(videoFilePath, ct);
        if (probeJson is null)
        {
            // Can't probe — assume transcode needed
            _logger.LogWarning("ffprobe failed for {Path}, falling back to transcode", videoFilePath);
            Console.Error.WriteLine($"[VIDEO-STRATEGY] FFPROBE_FAILED path={videoFilePath} → fallback to Transcode");
            return (StreamingStrategy.Transcode, null, null, null);
        }

        var (videoCodec, audioCodec, container) = ParseCodecInfo(probeJson);
        var strategy = _argBuilder.DecideStrategy(mimeType, videoCodec, audioCodec, container);

        _logger.LogInformation(
            "Streaming strategy for {Path}: {Strategy} (mime={Mime}, vcodec={VCodec}, acodec={ACodec}, container={Container})",
            videoFilePath, strategy, mimeType, videoCodec, audioCodec, container);

        Console.Error.WriteLine($"[VIDEO-STRATEGY] path={videoFilePath} strategy={strategy} mime={mimeType} vcodec={videoCodec} acodec={audioCodec} container={container}");

        return (strategy, videoCodec, audioCodec, container);
    }

    /// <inheritdoc />
    public async Task<bool> CanDirectPlayAsync(
        string videoFilePath,
        string mimeType,
        CancellationToken ct = default)
    {
        var (strategy, _, _, _) = await DecideStreamingStrategyAsync(videoFilePath, mimeType, ct);
        return strategy == StreamingStrategy.DirectPlay;
    }

    /// <inheritdoc />
    public async Task<(Process Process, string Args)> StreamCopyAsync(
        string sourceFilePath,
        string? videoCodec,
        string? audioCodec,
        CancellationToken ct = default)
    {
        var args = _argBuilder.GetStreamCopyArgs(sourceFilePath, videoCodec, audioCodec);

        _logger.LogInformation(
            "Starting stream copy (remux): source={Source}, vcodec={VCodec}, acodec={ACodec}, args={Args}",
            sourceFilePath, videoCodec, audioCodec, args);

        var psi = new ProcessStartInfo
        {
            FileName = _options.FfmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Capture stderr for error logging
        var stderrCapture = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrCapture.AppendLine(e.Data);
                _logger.LogDebug("ffmpeg(remux): {Line}", e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        // Store stderr capture for error handling
        process.Exited += (_, _) =>
        {
            if (process.ExitCode != 0)
            {
                var error = stderrCapture.ToString();
                _logger.LogError("ffmpeg stream copy failed (exit={ExitCode}): {Error}", process.ExitCode, error);
            }
        };

        return (process, args);
    }

    /// <inheritdoc />
    public async Task<string> StreamCopyToFileAsync(
        string sourceFilePath,
        string? videoCodec,
        string? audioCodec,
        CancellationToken ct = default)
    {
        // Create a temp output path for the remuxed file
        var tempDir = !string.IsNullOrWhiteSpace(_options.TempDirectory)
            ? _options.TempDirectory
            : Path.GetTempPath();
        var remuxDir = Path.Combine(tempDir, "dotnetcloud-remux");
        Directory.CreateDirectory(remuxDir);
        var outputPath = Path.Combine(remuxDir, $"remux-{Guid.NewGuid():N}.mp4");

        var args = _argBuilder.GetStreamCopyToFileArgs(sourceFilePath, outputPath, videoCodec, audioCodec);

        _logger.LogInformation(
            "Starting stream copy to file: source={Source}, output={Output}, vcodec={VCodec}, acodec={ACodec}",
            sourceFilePath, outputPath, videoCodec, audioCodec);

        var psi = new ProcessStartInfo
        {
            FileName = _options.FfmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderrCapture = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrCapture.AppendLine(e.Data);
                _logger.LogDebug("ffmpeg(remux-file): {Line}", e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);
        process.CancelErrorRead();

        if (process.ExitCode != 0)
        {
            var error = stderrCapture.ToString();
            _logger.LogError("ffmpeg stream copy to file failed (exit={ExitCode}): {Error}", process.ExitCode, error);
            throw new FfmpegException(
                $"ffmpeg stream copy failed with exit code {process.ExitCode}",
                process.ExitCode,
                error);
        }

        _logger.LogInformation("Stream copy to file completed: {Output}", outputPath);
        return outputPath;
    }

    /// <inheritdoc />
    public async Task<(string JobId, string OutputPath)> TranscodeAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null,
        CancellationToken ct = default)
    {
        // Check for existing active job (deduplication)
        var existingJob = _jobTracker.GetActiveJob(videoId, userId);
        if (existingJob is not null)
        {
            _logger.LogDebug("Reusing existing transcode job {JobId} for video {VideoId}", existingJob.Id, videoId);
            return (existingJob.Id, existingJob.OutputPath ?? string.Empty);
        }

        // Compute cache key
        var cacheKey = _cacheService.ComputeCacheKey(sourceFilePath, _options);

        // Check cache
        var cachedPath = _cacheService.GetCachedPath(cacheKey);
        if (cachedPath is not null)
        {
            _logger.LogDebug("Transcode cache hit for video {VideoId}, key {CacheKey}", videoId, cacheKey);
            var completedJob = _jobTracker.CreateJob(videoId, userId, cacheKey);
            completedJob.Status = TranscodingJobStatus.Completed;
            completedJob.ProgressPercent = 100.0;
            completedJob.OutputPath = cachedPath;
            completedJob.CompletedAt = DateTime.UtcNow;
            return (completedJob.Id, cachedPath);
        }

        // Acquire lock for this cache key to prevent concurrent transcodes of same file
        using var cacheLock = await _cacheService.LockCacheKeyAsync(cacheKey, ct);

        // Double-check cache after acquiring lock
        cachedPath = _cacheService.GetCachedPath(cacheKey);
        if (cachedPath is not null)
        {
            var completedJob = _jobTracker.CreateJob(videoId, userId, cacheKey);
            completedJob.Status = TranscodingJobStatus.Completed;
            completedJob.ProgressPercent = 100.0;
            completedJob.OutputPath = cachedPath;
            completedJob.CompletedAt = DateTime.UtcNow;
            return (completedJob.Id, cachedPath);
        }

        // Create job
        var job = _jobTracker.CreateJob(videoId, userId, cacheKey);

        // Determine output path for ffmpeg (temp location)
        var tempOutputDir = !string.IsNullOrWhiteSpace(_options.TempDirectory)
            ? _options.TempDirectory
            : Path.GetTempPath();
        var tempOutputPath = Path.Combine(tempOutputDir, $"transcode-{job.Id}.mp4");
        job.OutputPath = tempOutputPath;

        // Get video duration for progress tracking
        var duration = await GetVideoDurationAsync(sourceFilePath, ct);
        _logger.LogInformation("Transcode starting: job={JobId}, source={Source}, output={Output}, duration={Duration}, args={Options}",
            job.Id, sourceFilePath, tempOutputPath, duration,
            new { _options.VideoCodec, _options.VideoCrf, _options.EncoderPreset, _options.AudioCodec, _options.AudioBitrateKbps });

        // Build ffmpeg arguments
        var args = _argBuilder.BuildProgressiveMp4Args(
            sourceFilePath,
            tempOutputPath,
            _options,
            seekStart,
            seekDuration);

        // Run ffmpeg in background (don't await — let the caller poll progress)
        _ = Task.Run(async () =>
        {
            try
            {
                await _processManager.RunAsync(args, tempOutputPath, job, duration, ct);
                job.Status = TranscodingJobStatus.Completed;
                job.ProgressPercent = 100.0;
                job.CompletedAt = DateTime.UtcNow;
                _cacheService.RegisterCachedFile(cacheKey, tempOutputPath);
                job.OutputPath = _cacheService.GetCacheFilePath(cacheKey);
                _logger.LogInformation("Transcode job {JobId} completed and cached", job.Id);
            }
            catch (FfmpegException ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.FfmpegError ?? ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Transcode job {JobId} failed (exit={ExitCode}): {FfmpegError}", job.Id, ex.ExitCode, ex.FfmpegError ?? ex.Message);
            }
            catch (OperationCanceledException)
            {
                job.Status = TranscodingJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Transcode job {JobId} cancelled", job.Id);
            }
            catch (Exception ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Transcode job {JobId} unexpected error", job.Id);
            }
        }, ct);

        return (job.Id, tempOutputPath);
    }

    /// <inheritdoc />
    public TranscodingJob? GetProgress(string jobId)
    {
        return _jobTracker.GetJob(jobId);
    }

    /// <inheritdoc />
    public void CancelTranscode(string jobId)
    {
        _processManager.CancelJob(jobId);
    }

    /// <inheritdoc />
    public async Task<(string JobId, string OutputDir, string PlaylistPath)> TranscodeHlsAsync(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        string? sourceVideoCodec = null,
        string? sourceAudioCodec = null,
        CancellationToken ct = default)
    {
        // Acquire per-video lock to prevent duplicate ffmpeg processes
        var hlsLock = await _jobTracker.AcquireHlsLockAsync(videoId);
        if (hlsLock is null)
        {
            // Another request beat us to creating the job — use theirs
            var existingJob = _jobTracker.GetActiveHlsJob(videoId);
            if (existingJob is not null)
            {
                _logger.LogDebug("Reusing HLS transcode job {JobId} created by concurrent request for video {VideoId}", existingJob.Id, videoId);
                var existingDir = Path.GetDirectoryName(existingJob.OutputPath)
                                  ?? Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}");
                var existingPlaylist = Path.Combine(existingDir, "playlist.m3u8");
                return (existingJob.Id, existingDir, existingPlaylist);
            }

            // Lock timed out (30s) — extremely rare, fall back to lock-free creation
            _logger.LogWarning("HLS lock acquire timed out for video {VideoId}; proceeding without lock", videoId);
            return await CreateHlsJobUnlocked(videoId, userId, sourceFilePath, mimeType, sourceVideoCodec, sourceAudioCodec, ct);
        }

        // Variables captured outside the lock for return
        string actualOutputDir;
        string actualPlaylistPath;

        using (hlsLock)
        {
            // Double-check inside the lock
            var existingJob = _jobTracker.GetActiveHlsJob(videoId);
            if (existingJob is not null)
            {
                _logger.LogDebug("Reusing existing HLS transcode job {JobId} for video {VideoId}", existingJob.Id, videoId);
                var existingDir = Path.GetDirectoryName(existingJob.OutputPath)
                                  ?? Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}");
                var existingPlaylist = Path.Combine(existingDir, "playlist.m3u8");
                return (existingJob.Id, existingDir, existingPlaylist);
            }

            // Create fresh HLS output directory (delete stale files from previous transcode)
            actualOutputDir = Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}-{userId:N}");
            if (Directory.Exists(actualOutputDir))
            {
                _logger.LogDebug("Cleaning stale HLS output directory: {Dir}", actualOutputDir);
                try
                { Directory.Delete(actualOutputDir, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to clean stale HLS dir"); }
            }
            Directory.CreateDirectory(actualOutputDir);
            actualPlaylistPath = Path.Combine(actualOutputDir, "playlist.m3u8");

            // Create job (no cache for HLS — segments are transient)
            var job = _jobTracker.CreateJob(videoId, userId, $"hls-{videoId:N}");
            job.OutputPath = actualPlaylistPath;
            job.IsHls = true;

            // Lock released here — job is now visible to concurrent requests via GetActiveHlsJob.
            // ffmpeg launch below is outside the lock (fire-and-forget).
        }

        // Re-read job to get the one we just created
        var activeJob = _jobTracker.GetActiveHlsJob(videoId);
        if (activeJob is null)
        {
            throw new InvalidOperationException($"HLS job was created but not found for video {videoId}");
        }

        // Launch ffmpeg (outside lock, fire-and-forget)
        await LaunchFfmpegAsync(activeJob, sourceFilePath, actualOutputDir, actualPlaylistPath, sourceVideoCodec, sourceAudioCodec, ct);

        return (activeJob.Id, actualOutputDir, actualPlaylistPath);
    }

    /// <inheritdoc />
    public TranscodingJob? GetActiveHlsJob(Guid videoId)
    {
        return _jobTracker.GetActiveHlsJob(videoId);
    }

    /// <summary>
    /// Creates an HLS transcode job WITHOUT acquiring the per-video lock.
    /// Only used as a fallback when lock acquisition times out (extremely rare).
    /// </summary>
    private async Task<(string JobId, string OutputDir, string PlaylistPath)> CreateHlsJobUnlocked(
        Guid videoId,
        Guid userId,
        string sourceFilePath,
        string mimeType,
        string? sourceVideoCodec,
        string? sourceAudioCodec,
        CancellationToken ct)
    {
        // Create fresh HLS output directory
        var actualOutputDir = Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}-{userId:N}");
        if (Directory.Exists(actualOutputDir))
        {
            try
            { Directory.Delete(actualOutputDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to clean stale HLS dir"); }
        }
        Directory.CreateDirectory(actualOutputDir);
        var actualPlaylistPath = Path.Combine(actualOutputDir, "playlist.m3u8");

        // Create job
        var job = _jobTracker.CreateJob(videoId, userId, $"hls-{videoId:N}");
        job.OutputPath = actualPlaylistPath;
        job.IsHls = true;

        // Launch ffmpeg
        await LaunchFfmpegAsync(job, sourceFilePath, actualOutputDir, actualPlaylistPath, sourceVideoCodec, sourceAudioCodec, ct);

        return (job.Id, actualOutputDir, actualPlaylistPath);
    }

    /// <summary>
    /// Launches ffmpeg as a background task for HLS transcoding.
    /// </summary>
    private async Task LaunchFfmpegAsync(
        TranscodingJob job,
        string sourceFilePath,
        string outputDir,
        string playlistPath,
        string? sourceVideoCodec,
        string? sourceAudioCodec,
        CancellationToken ct)
    {
        var duration = await GetVideoDurationAsync(sourceFilePath, ct);
        _logger.LogInformation("HLS transcode starting: job={JobId}, source={Source}, outputDir={OutputDir}, duration={Duration}",
            job.Id, sourceFilePath, outputDir, duration);

        var args = _argBuilder.BuildHlsArgs(sourceFilePath, outputDir, _options, sourceVideoCodec, sourceAudioCodec);

        _ = Task.Run(async () =>
        {
            try
            {
                await _processManager.RunAsync(args, playlistPath, job, duration, ct);
                job.Status = TranscodingJobStatus.Completed;
                job.ProgressPercent = 100.0;
                job.CompletedAt = DateTime.UtcNow;

                // Append #EXT-X-ENDLIST to signal HLS playlist completion.
                // With -hls_playlist_type event, ffmpeg writes segments progressively
                // but doesn't add the ENDLIST tag. hls.js needs it to stop polling.
                try
                {
                    await File.AppendAllTextAsync(playlistPath, "#EXT-X-ENDLIST\n");
                    _logger.LogDebug("HLS transcode job {JobId}: appended #EXT-X-ENDLIST to playlist", job.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HLS transcode job {JobId}: failed to append #EXT-X-ENDLIST", job.Id);
                }

                _logger.LogInformation("HLS transcode job {JobId} completed", job.Id);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    CleanupHlsDirectory(outputDir, job.Id);
                    TryDeleteFile(sourceFilePath);
                });
            }
            catch (FfmpegException ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.FfmpegError ?? ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "HLS transcode job {JobId} failed (exit={ExitCode}): {FfmpegError}", job.Id, ex.ExitCode, ex.FfmpegError ?? ex.Message);
                CleanupHlsDirectory(outputDir, job.Id);
                TryDeleteFile(sourceFilePath);
            }
            catch (OperationCanceledException)
            {
                job.Status = TranscodingJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("HLS transcode job {JobId} cancelled", job.Id);
                CleanupHlsDirectory(outputDir, job.Id);
                TryDeleteFile(sourceFilePath);
            }
            catch (Exception ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "HLS transcode job {JobId} unexpected error", job.Id);
                CleanupHlsDirectory(outputDir, job.Id);
                TryDeleteFile(sourceFilePath);
            }
        }, ct);
    }

    // ─── Private Helpers ────────────────────────────────────────────────

    private async Task<string?> RunFfprobeAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var ffprobePath = ResolveFfprobePath();
            var args = _argBuilder.BuildFfprobeArgs(filePath);

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {Path}", filePath);
            Console.Error.WriteLine($"[VIDEO-FFPROBE-FAIL] path={filePath} error={ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private string ResolveFfprobePath()
    {
        var ffmpegPath = _options.FfmpegPath;
        if (ffmpegPath.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            // Replace the "ffmpeg" suffix with "ffprobe" — e.g. /usr/bin/ffmpeg → /usr/bin/ffprobe
            return ffmpegPath[..^6] + "ffprobe";
        }

        if (ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ffmpegPath[..^10] + "ffprobe.exe";
        }

        return "ffprobe";
    }

    private static (string? VideoCodec, string? AudioCodec, string? Container) ParseCodecInfo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? videoCodec = null;
        string? audioCodec = null;
        string? container = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;
                var codecName = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;

                if (codecType == "video" && videoCodec is null)
                    videoCodec = codecName;
                if (codecType == "audio" && audioCodec is null)
                    audioCodec = codecName;
            }
        }

        if (root.TryGetProperty("format", out var format) &&
            format.TryGetProperty("format_name", out var fmtName))
        {
            container = fmtName.GetString()?.Split(',').FirstOrDefault();
        }

        return (videoCodec, audioCodec, container);
    }

    private async Task<TimeSpan> GetVideoDurationAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var json = await RunFfprobeAsync(filePath, ct);
            if (json is null)
                return TimeSpan.Zero;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var dur) &&
                double.TryParse(dur.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch
        {
            // ignore
        }

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Returns the root directory for HLS transcode output.
    /// </summary>
    private string GetHlsRootDir()
    {
        return !string.IsNullOrWhiteSpace(_options.TempDirectory)
            ? Path.Combine(_options.TempDirectory, "hls")
            : Path.Combine(Path.GetTempPath(), "dotnetcloud-hls");
    }

    /// <summary>
    /// Deletes an HLS segment directory and all its contents.
    /// Best-effort — failures are logged but not thrown.
    /// </summary>
    private void CleanupHlsDirectory(string dir, string jobId)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
                _logger.LogDebug("Cleaned up HLS directory for job {JobId}: {Dir}", jobId, dir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up HLS directory for job {JobId}: {Dir}", jobId, dir);
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch { /* best effort */ }
    }
}
