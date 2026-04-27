using System.Diagnostics;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video.Models;
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
            // Resolve the owner so admin shared folders (_DotNetCloud/Movies) are reachable
            var ownerId = await _db.Videos.IgnoreQueryFilters()
                .Where(v => v.Id == videoId)
                .Select(v => v.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            // Download the video file to a temp location
            var caller = new CallerContext(ownerId, [], CallerType.System);
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
                try { File.Delete(tempFramePath); } catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete temp frame file {Path}", tempFramePath); }
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
            var ownerId = await _db.Videos.IgnoreQueryFilters()
                .Where(v => v.Id == videoId)
                .Select(v => v.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            var caller = new CallerContext(ownerId, [], CallerType.System);
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
                        try { File.Delete(frameTemp); } catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete screenshot temp file {Path}", frameTemp); }
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate screenshot directory for video {VideoId}", videoId);
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }
    }

    /// <inheritdoc />
    public async Task ExtractMetadataAsync(Guid videoId, Guid fileNodeId, CancellationToken cancellationToken = default)
    {
        string? tempVideoPath = null;

        try
        {
            var ownerId = await _db.Videos.IgnoreQueryFilters()
                .Where(v => v.Id == videoId)
                .Select(v => v.OwnerId)
                .FirstOrDefaultAsync(cancellationToken);

            var caller = new CallerContext(ownerId, [], CallerType.System);
            await using var videoStream = await _downloadService.DownloadCurrentAsync(fileNodeId, caller);

            if (videoStream is FileStream fs)
                tempVideoPath = fs.Name;
            else
            {
                tempVideoPath = Path.GetTempFileName();
                await using var tempFile = File.Create(tempVideoPath);
                await videoStream.CopyToAsync(tempFile, cancellationToken);
            }

            // Run ffprobe — derive path from ffmpegthumbnailer location
            var ffprobePath = _ffmpegPath == "ffmpegthumbnailer"
                ? "ffprobe"
                : Path.Combine(Path.GetDirectoryName(_ffmpegPath)!, "ffprobe");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("quiet");
            startInfo.ArgumentList.Add("-print_format");
            startInfo.ArgumentList.Add("json");
            startInfo.ArgumentList.Add("-show_format");
            startInfo.ArgumentList.Add("-show_streams");
            startInfo.ArgumentList.Add(tempVideoPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Unable to start ffprobe for video {VideoId}", videoId);
                return;
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("ffprobe failed for video {VideoId}: {StdErr}", videoId, stderr);
                return;
            }

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            var streams = root.TryGetProperty("streams", out var s) ? s : default;
            var format = root.TryGetProperty("format", out var f) ? f : default;

            var vid = EnumerateStreams(streams, "video");
            var aud = EnumerateStreams(streams, "audio");

            var metadata = new VideoMetadata
            {
                VideoId = videoId,
                Width = vid.w ?? 0,
                Height = vid.h ?? 0,
                FrameRate = ParseFrameRate(vid.r),
                VideoCodec = vid.c,
                AudioCodec = aud.c,
                Bitrate = ParseLong(vid.b) ?? ParseLong(format, "bit_rate") ?? 0,
                AudioTrackCount = CountStreams(streams, "audio"),
                SubtitleTrackCount = CountStreams(streams, "subtitle"),
                ContainerFormat = GetString(format, "format_name")?.Split(',').FirstOrDefault()?.Trim()
            };

            // Save or update
            var existing = await _db.VideoMetadata
                .FirstOrDefaultAsync(m => m.VideoId == videoId, cancellationToken);

            if (existing is not null)
            {
                existing.Width = metadata.Width;
                existing.Height = metadata.Height;
                existing.FrameRate = metadata.FrameRate;
                existing.VideoCodec = metadata.VideoCodec;
                existing.AudioCodec = metadata.AudioCodec;
                existing.Bitrate = metadata.Bitrate;
                existing.AudioTrackCount = metadata.AudioTrackCount;
                existing.SubtitleTrackCount = metadata.SubtitleTrackCount;
                existing.ContainerFormat = metadata.ContainerFormat;
                existing.ExtractedAt = DateTime.UtcNow;
            }
            else
            {
                metadata.ExtractedAt = DateTime.UtcNow;
                _db.VideoMetadata.Add(metadata);
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Metadata extracted for video {VideoId}: {Width}x{Height} {Codec}",
                videoId, metadata.Width, metadata.Height, metadata.VideoCodec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata extraction failed for video {VideoId}", videoId);
        }
    }

    private static (int? w, int? h, string? c, string? r, string? b) EnumerateStreams(JsonElement streams, string type)
    {
        if (streams.ValueKind != JsonValueKind.Array) return default;

        foreach (var stream in streams.EnumerateArray())
        {
            if (stream.TryGetProperty("codec_type", out var ct) &&
                ct.ValueKind == JsonValueKind.String &&
                string.Equals(ct.GetString(), type, StringComparison.OrdinalIgnoreCase))
            {
                int? w = null, h = null;
                string? c = null, r = null, b = null;

                if (stream.TryGetProperty("width", out var wEl) && wEl.TryGetInt32(out var wVal)) w = wVal;
                if (stream.TryGetProperty("height", out var hEl) && hEl.TryGetInt32(out var hVal)) h = hVal;
                if (stream.TryGetProperty("codec_name", out var cn) && cn.ValueKind == JsonValueKind.String) c = cn.GetString();
                if (stream.TryGetProperty("r_frame_rate", out var rf) && rf.ValueKind == JsonValueKind.String) r = rf.GetString();
                if (stream.TryGetProperty("bit_rate", out var br) && br.ValueKind == JsonValueKind.String) b = br.GetString();

                return (w, h, c, r, b);
            }
        }

        return default;
    }

    private static int CountStreams(JsonElement streams, string type)
    {
        if (streams.ValueKind != JsonValueKind.Array) return 0;
        var count = 0;
        foreach (var stream in streams.EnumerateArray())
        {
            if (stream.TryGetProperty("codec_type", out var ct) &&
                ct.ValueKind == JsonValueKind.String &&
                string.Equals(ct.GetString(), type, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
    }

    private static double ParseFrameRate(string? rFrameRate)
    {
        if (string.IsNullOrWhiteSpace(rFrameRate)) return 0;
        var parts = rFrameRate.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var num) &&
            double.TryParse(parts[1], out var den) &&
            den > 0)
        {
            return Math.Round(num / den, 2);
        }
        return double.TryParse(rFrameRate, out var d) ? Math.Round(d, 2) : 0;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, out var v) ? v : null;
    }

    private static long? ParseLong(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.String &&
               long.TryParse(prop.GetString(), out var v) ? v : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
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
