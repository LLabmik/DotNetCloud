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
    public async Task<bool> CanDirectPlayAsync(
        string videoFilePath,
        string mimeType,
        CancellationToken ct = default)
    {
        // Fast check: MIME type must be video/mp4
        if (!string.Equals(mimeType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // Run ffprobe to get codec info
        var probeJson = await RunFfprobeAsync(videoFilePath, ct);
        if (probeJson is null)
            return false;

        var (videoCodec, audioCodec, container) = ParseCodecInfo(probeJson);
        return _argBuilder.CanDirectPlay(mimeType, videoCodec, audioCodec, container ?? string.Empty);
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
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "Transcode job {JobId} failed: {Error}", job.Id, ex.Message);
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
        CancellationToken ct = default)
    {
        // Check for existing active HLS job (deduplication)
        var existingJob = _jobTracker.GetActiveHlsJob(videoId);
        if (existingJob is not null)
        {
            _logger.LogDebug("Reusing existing HLS transcode job {JobId} for video {VideoId}", existingJob.Id, videoId);
            var existingDir = Path.GetDirectoryName(existingJob.OutputPath)
                              ?? Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}");
            var existingPlaylist = Path.Combine(existingDir, "playlist.m3u8");
            return (existingJob.Id, existingDir, existingPlaylist);
        }

        // Create HLS output directory
        var outputDir = Path.Combine(GetHlsRootDir(), $"hls-{videoId:N}-{userId:N}");
        Directory.CreateDirectory(outputDir);
        var playlistPath = Path.Combine(outputDir, "playlist.m3u8");

        // Create job (no cache for HLS — segments are transient)
        var job = _jobTracker.CreateJob(videoId, userId, $"hls-{videoId:N}");
        job.OutputPath = playlistPath;
        job.IsHls = true;

        // Get video duration for progress tracking
        var duration = await GetVideoDurationAsync(sourceFilePath, ct);
        _logger.LogInformation("HLS transcode starting: job={JobId}, source={Source}, outputDir={OutputDir}, duration={Duration}",
            job.Id, sourceFilePath, outputDir, duration);

        // Build ffmpeg HLS arguments
        var args = _argBuilder.BuildHlsArgs(sourceFilePath, outputDir, _options);

        // Run ffmpeg in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _processManager.RunAsync(args, playlistPath, job, duration, ct);
                job.Status = TranscodingJobStatus.Completed;
                job.ProgressPercent = 100.0;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("HLS transcode job {JobId} completed", job.Id);

                // Schedule segment cleanup after a short delay (let any in-flight
                // segment requests finish)
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    CleanupHlsDirectory(outputDir, job.Id);
                });
            }
            catch (FfmpegException ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "HLS transcode job {JobId} failed: {Error}", job.Id, ex.Message);
                CleanupHlsDirectory(outputDir, job.Id);
            }
            catch (OperationCanceledException)
            {
                job.Status = TranscodingJobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("HLS transcode job {JobId} cancelled", job.Id);
                CleanupHlsDirectory(outputDir, job.Id);
            }
            catch (Exception ex)
            {
                job.Status = TranscodingJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(ex, "HLS transcode job {JobId} unexpected error", job.Id);
                CleanupHlsDirectory(outputDir, job.Id);
            }
        }, ct);

        return (job.Id, outputDir, playlistPath);
    }

    /// <inheritdoc />
    public TranscodingJob? GetActiveHlsJob(Guid videoId)
    {
        return _jobTracker.GetActiveHlsJob(videoId);
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
            return null;
        }
    }

    private string ResolveFfprobePath()
    {
        var ffmpegPath = _options.FfmpegPath;
        if (ffmpegPath.EndsWith("ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return ffmpegPath[..^6] + "ffprobe" + ffmpegPath[^6..].Replace("ffmpeg", "ffprobe");
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
}
