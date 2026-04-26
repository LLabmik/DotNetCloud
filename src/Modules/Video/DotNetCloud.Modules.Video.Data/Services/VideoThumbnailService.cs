using System.Diagnostics;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Generates and stores video poster thumbnails by extracting a frame via FFmpeg
/// and resizing with ImageSharp. Thumbnails are stored as JPEG bytes in the database.
/// </summary>
public sealed class VideoThumbnailService : IVideoThumbnailService
{
    private const int PosterWidth = 300;
    private const int JpegQuality = 80;

    private readonly VideoDbContext _db;
    private readonly IDownloadService _downloadService;
    private readonly string _ffmpegPath;
    private readonly string _screenshotCacheDir;
    private readonly string _posterCacheDir;
    private readonly ILogger<VideoThumbnailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoThumbnailService"/> class.
    /// </summary>
    public VideoThumbnailService(
        VideoDbContext db,
        IDownloadService downloadService,
        IConfiguration configuration,
        ILogger<VideoThumbnailService> logger)
    {
        _db = db;
        _downloadService = downloadService;
        _ffmpegPath = configuration["Video:Thumbnails:FfmpegPath"] ?? "ffmpegthumbnailer";
        _logger = logger;

        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        _screenshotCacheDir = Path.Combine(storageRoot, ".video-screenshots");
        _posterCacheDir = Path.Combine(storageRoot, ".video-posters");
    }

    /// <inheritdoc />
    public async Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        // Priority 1: TMDB cached poster
        var enriched = await _db.Videos.IgnoreQueryFilters()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.HasExternalPoster, v.ExternalPosterPath })
            .FirstOrDefaultAsync(cancellationToken);

        if (enriched?.HasExternalPoster == true &&
            enriched.ExternalPosterPath is not null &&
            File.Exists(enriched.ExternalPosterPath))
        {
            return (File.OpenRead(enriched.ExternalPosterPath), "image/jpeg");
        }

        // Priority 2: Generated screenshots
        var screenshotPaths = await GetScreenshotPathsAsync(videoId, cancellationToken);
        if (screenshotPaths is { Count: > 0 })
        {
            return (File.OpenRead(screenshotPaths[0]), "image/jpeg");
        }

        // Priority 3: DB poster (existing ffmpegthumbnailer frame)
        var data = await _db.Videos.IgnoreQueryFilters()
            .Where(v => v.Id == videoId)
            .Select(v => v.ThumbnailPoster)
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null || data.Length == 0)
            return (null, null);

        return (new MemoryStream(data, writable: false), "image/jpeg");
    }

    /// <inheritdoc />
    public async Task GenerateThumbnailAsync(
        Guid videoId,
        Guid fileNodeId,
        CancellationToken cancellationToken = default)
    {
        string? tempVideoPath = null;
        string? tempFramePath = null;

        try
        {
            // Download the video file to a temp location
            var caller = new CallerContext(Guid.Empty, [], CallerType.System);
            await using var videoStream = await _downloadService.DownloadCurrentAsync(fileNodeId, caller);

            // If it's a FileStream (DeleteOnClose temp file from download service), use it directly
            if (videoStream is FileStream fs)
            {
                tempVideoPath = fs.Name;
            }
            else
            {
                // Copy to a temp file for FFmpeg (it needs a seekable file path)
                tempVideoPath = Path.GetTempFileName();
                await using var tempFile = File.Create(tempVideoPath);
                await videoStream.CopyToAsync(tempFile, cancellationToken);
            }

            // Extract a frame at ~2 seconds (falls back to first frame for short videos)
            tempFramePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
            var extracted = await ExtractFrameAsync(tempVideoPath, tempFramePath, "10%", cancellationToken);
            if (!extracted)
            {
                _logger.LogWarning("ffmpegthumbnailer frame extraction failed for video {VideoId}", videoId);
                return;
            }

            // Resize to poster width and encode as JPEG
            byte[] posterBytes;
            await using (var frameStream = File.OpenRead(tempFramePath))
            {
                using var image = await Image.LoadAsync(frameStream, cancellationToken);
                var ratio = (double)PosterWidth / image.Width;
                var newHeight = (int)(image.Height * ratio);

                image.Mutate(x => x.Resize(PosterWidth, newHeight));

                using var output = new MemoryStream();
                var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = JpegQuality };
                await image.SaveAsync(output, encoder, cancellationToken);
                posterBytes = output.ToArray();
            }

            // Store in DB
            var video = await _db.Videos.IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

            if (video is null)
            {
                _logger.LogWarning("Video {VideoId} not found for thumbnail storage", videoId);
                return;
            }

            video.ThumbnailPoster = posterBytes;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Video thumbnail generated for {VideoId} ({Size} bytes)", videoId, posterBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for video {VideoId}", videoId);
        }
        finally
        {
            // Clean up temp frame file (video temp file is DeleteOnClose from download service)
            if (tempFramePath is not null && File.Exists(tempFramePath))
            {
                try { File.Delete(tempFramePath); } catch { /* best effort */ }
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteThumbnailAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video is not null)
        {
            video.ThumbnailPoster = null;

            // Clean up cached poster on disk
            if (video.ExternalPosterPath is not null && File.Exists(video.ExternalPosterPath))
            {
                try { File.Delete(video.ExternalPosterPath); } catch { /* best effort */ }
                video.ExternalPosterPath = null;
            }
            video.HasExternalPoster = false;

            await _db.SaveChangesAsync(cancellationToken);
        }

        // Clean up screenshots on disk
        var screenshotPaths = await GetScreenshotPathsAsync(videoId, cancellationToken);
        if (screenshotPaths is not null)
        {
            foreach (var path in screenshotPaths)
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        _logger.LogDebug("Video thumbnail deleted for {VideoId}", videoId);
    }

    /// <inheritdoc />
    public async Task GenerateScreenshotsAsync(Guid videoId, Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        string? tempVideoPath = null;

        try
        {
            var caller = new CallerContext(Guid.Empty, [], CallerType.System);
            await using var videoStream = await _downloadService.DownloadCurrentAsync(fileNodeId, caller);

            if (videoStream is FileStream fs)
                tempVideoPath = fs.Name;
            else
            {
                tempVideoPath = Path.GetTempFileName();
                await using var tempFile = File.Create(tempVideoPath);
                await videoStream.CopyToAsync(tempFile, cancellationToken);
            }

            // Extract frames at multiple timestamps
            foreach (var pct in new[] { 10, 30, 50, 70, 90 })
            {
                var frameTemp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
                try
                {
                    var extracted = await ExtractFrameAsync(tempVideoPath, frameTemp, $"{pct}%", cancellationToken);
                    if (!extracted) continue;

                    byte[] screenshotBytes;
                    await using (var frameStream = File.OpenRead(frameTemp))
                    {
                        using var image = await Image.LoadAsync(frameStream, cancellationToken);
                        var ratio = (double)PosterWidth / image.Width;
                        var newHeight = (int)(image.Height * ratio);
                        image.Mutate(x => x.Resize(PosterWidth, newHeight));

                        using var output = new MemoryStream();
                        var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = JpegQuality };
                        await image.SaveAsync(output, encoder, cancellationToken);
                        screenshotBytes = output.ToArray();
                    }

                    Directory.CreateDirectory(_screenshotCacheDir);
                    var screenshotPath = Path.Combine(_screenshotCacheDir, $"{videoId}_{pct}.jpg");
                    await File.WriteAllBytesAsync(screenshotPath, screenshotBytes, cancellationToken);
                }
                finally
                {
                    if (File.Exists(frameTemp))
                    {
                        try { File.Delete(frameTemp); } catch { /* best effort */ }
                    }
                }
            }

            _logger.LogInformation("Screenshots generated for video {VideoId}", videoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate screenshots for video {VideoId}", videoId);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>?> GetScreenshotPathsAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_screenshotCacheDir))
                return Task.FromResult<IReadOnlyList<string>?>(null);

            var prefix = $"{videoId}_";
            var files = Directory.GetFiles(_screenshotCacheDir, $"{prefix}*.jpg")
                .OrderBy(f => f)
                .ToList();

            return Task.FromResult<IReadOnlyList<string>?>(files.Count > 0 ? files : null);
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }
    }

    private async Task<bool> ExtractFrameAsync(string inputPath, string outputPath, string timestamp, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            // ffmpegthumbnailer: -i input -o output -s size -t time% -q quality -c format
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add("0");  // original size (ImageSharp will resize)
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(timestamp);
            startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add("8");  // quality 0-10
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("jpeg");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Unable to start ffmpegthumbnailer process for video thumbnail generation.");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("ffmpegthumbnailer exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return false;
            }

            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpegthumbnailer frame extraction failed for input {InputPath}.", inputPath);
            return false;
        }
    }
}
