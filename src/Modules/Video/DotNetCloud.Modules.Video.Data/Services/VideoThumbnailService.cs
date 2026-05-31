using System.Diagnostics;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Storage;
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
/// and resizing with ImageSharp. Thumbnails are stored in content-addressed storage.
/// </summary>
public sealed class VideoThumbnailService : IVideoThumbnailService
{
    private const int PosterWidth = 300;
    private const int JpegQuality = 80;

    private readonly VideoDbContext _db;
    private readonly IDownloadService _downloadService;
    private readonly ContentAddressedStorage _contentStorage;
    private readonly string _ffmpegPath;
    private readonly string _screenshotCacheDir;
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
        _contentStorage = new ContentAddressedStorage(storageRoot);
    }

    /// <inheritdoc />
    public async Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        // Priority 1: Content-addressed storage via CanonicalVideo.ThumbnailPosterHash
        var canonicalHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalVideo!.ThumbnailPosterHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(canonicalHash))
        {
            var casPath = _contentStorage.GetPath(canonicalHash, ".jpg");
            if (File.Exists(casPath))
            {
                return (File.OpenRead(casPath), "image/jpeg");
            }
        }

        // Priority 2: External poster from canonical enrichment
        var enriched = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => new
            {
                HasExternalPoster = uv.CanonicalVideo != null && uv.CanonicalVideo.HasExternalPoster,
                ExternalPosterHash = uv.CanonicalVideo != null ? uv.CanonicalVideo.ExternalPosterHash : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (enriched?.HasExternalPoster == true && enriched.ExternalPosterHash is not null)
        {
            var casPath = _contentStorage.GetPath(enriched.ExternalPosterHash, ".jpg");
            if (File.Exists(casPath))
            {
                return (File.OpenRead(casPath), "image/jpeg");
            }
        }

        // Priority 3: Generated screenshots
        var screenshotPaths = await GetScreenshotPathsAsync(videoId, cancellationToken);
        if (screenshotPaths is { Count: > 0 })
        {
            return (File.OpenRead(screenshotPaths[0]), "image/jpeg");
        }

        return (null, null);
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
            var ownerId = await _db.UserVideos
                .Where(uv => uv.Id == videoId)
                .Select(uv => uv.OwnerId)
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

            // ── Store in content-addressed storage ──
            var posterHash = _contentStorage.Store(posterBytes, ".jpg");

            // ── Update CanonicalVideo.ThumbnailPosterHash ──
            var userVideo = await _db.UserVideos
                .Include(uv => uv.CanonicalVideo)
                .FirstOrDefaultAsync(uv => uv.Id == videoId, cancellationToken);

            if (userVideo?.CanonicalVideo is not null)
            {
                userVideo.CanonicalVideo.ThumbnailPosterHash = posterHash;
                userVideo.CanonicalVideo.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Video thumbnail generated for {VideoId} ({Size} bytes, hash={Hash})",
                videoId, posterBytes.Length, posterHash);
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
                try
                { File.Delete(tempFramePath); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete temp frame file {Path}", tempFramePath); }
            }
        }
    }

    /// <inheritdoc />
    public async Task DeleteThumbnailAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        // ── Clear canonical poster hash ──
        var userVideo = await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId, cancellationToken);

        if (userVideo?.CanonicalVideo is not null)
        {
            var oldHash = userVideo.CanonicalVideo.ThumbnailPosterHash;
            userVideo.CanonicalVideo.ThumbnailPosterHash = null;
            userVideo.CanonicalVideo.UpdatedAt = DateTime.UtcNow;

            // Clean up content-addressed file
            if (!string.IsNullOrEmpty(oldHash))
            {
                try
                { _contentStorage.Delete(oldHash); }
                catch { /* best effort */ }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Clean up screenshots on disk
        var screenshotPaths = await GetScreenshotPathsAsync(videoId, cancellationToken);
        if (screenshotPaths is not null)
        {
            foreach (var path in screenshotPaths)
            {
                try
                { File.Delete(path); }
                catch { /* best effort */ }
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
            var ownerId = await _db.UserVideos
                .Where(uv => uv.Id == videoId)
                .Select(uv => uv.OwnerId)
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
                    if (!extracted)
                        continue;

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
                        try
                        { File.Delete(frameTemp); }
                        catch (Exception ex) { _logger.LogDebug(ex, "Failed to delete screenshot temp file {Path}", frameTemp); }
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
            var ownerId = await _db.UserVideos
                .Where(uv => uv.Id == videoId)
                .Select(uv => uv.OwnerId)
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

            // ── Extract container format tags for CanonicalVideo ──
            var formatTags = format.ValueKind == JsonValueKind.Object && format.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object
                ? tags
                : default(JsonElement);

            string? embeddedTitle = null;
            string? embeddedImdbId = null;
            int? embeddedTmdbId = null;
            string? embeddedDate = null;
            string? embeddedLanguage = null;

            if (formatTags.ValueKind == JsonValueKind.Object)
            {
                embeddedTitle = GetString(formatTags, "title");
                embeddedImdbId = GetString(formatTags, "IMDB") ?? GetString(formatTags, "imdb");
                if (int.TryParse(GetString(formatTags, "TMDB") ?? GetString(formatTags, "tmdb"), out var tmdbId))
                    embeddedTmdbId = tmdbId;
                embeddedDate = GetString(formatTags, "date") ?? GetString(formatTags, "creation_time");
                embeddedLanguage = GetString(formatTags, "language");
            }

            // ── Update CanonicalVideo with embedded metadata tags ──
            var userVideo = await _db.UserVideos
                .Include(uv => uv.CanonicalVideo)
                .FirstOrDefaultAsync(uv => uv.Id == videoId, cancellationToken);

            if (userVideo?.CanonicalVideo is not null)
            {
                var canonical = userVideo.CanonicalVideo;
                if (embeddedTitle is not null)
                    canonical.EmbeddedTitle = embeddedTitle;
                if (embeddedImdbId is not null)
                    canonical.EmbeddedImdbId = embeddedImdbId;
                if (embeddedTmdbId.HasValue)
                    canonical.EmbeddedTmdbId = embeddedTmdbId;
                if (embeddedDate is not null)
                    canonical.EmbeddedDate = embeddedDate;
                if (embeddedLanguage is not null)
                    canonical.EmbeddedLanguage = embeddedLanguage;
                canonical.UpdatedAt = DateTime.UtcNow;
            }

            // ── Store metadata on CanonicalVideoMetadata ──
            if (userVideo?.CanonicalVideo is not null)
            {
                var canonicalMetadata = await _db.CanonicalVideoMetadata
                    .FirstOrDefaultAsync(cm => cm.VideoContentHash == userVideo.CanonicalContentHash, cancellationToken);

                var width = vid.w ?? 0;
                var height = vid.h ?? 0;
                var frameRate = ParseFrameRate(vid.r);
                var videoCodec = vid.c;
                var audioCodec = aud.c;
                var bitrate = ParseLong(vid.b) ?? ParseLong(format, "bit_rate") ?? 0;
                var audioTrackCount = CountStreams(streams, "audio");
                var subtitleTrackCount = CountStreams(streams, "subtitle");
                var containerFormat = GetString(format, "format_name")?.Split(',').FirstOrDefault()?.Trim();

                if (canonicalMetadata is not null)
                {
                    canonicalMetadata.Width = width;
                    canonicalMetadata.Height = height;
                    canonicalMetadata.FrameRate = frameRate;
                    canonicalMetadata.VideoCodec = videoCodec;
                    canonicalMetadata.AudioCodec = audioCodec;
                    canonicalMetadata.Bitrate = bitrate;
                    canonicalMetadata.AudioTrackCount = audioTrackCount;
                    canonicalMetadata.SubtitleTrackCount = subtitleTrackCount;
                    canonicalMetadata.ContainerFormat = containerFormat;
                    canonicalMetadata.ExtractedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.CanonicalVideoMetadata.Add(new CanonicalVideoMetadata
                    {
                        VideoContentHash = userVideo.CanonicalContentHash,
                        Width = width,
                        Height = height,
                        FrameRate = frameRate,
                        VideoCodec = videoCodec,
                        AudioCodec = audioCodec,
                        Bitrate = bitrate,
                        AudioTrackCount = audioTrackCount,
                        SubtitleTrackCount = subtitleTrackCount,
                        ContainerFormat = containerFormat,
                        ExtractedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Metadata extracted for video {VideoId}: {Width}x{Height} {Codec}",
                    videoId, width, height, videoCodec);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata extraction failed for video {VideoId}", videoId);
        }
    }

    private static (int? w, int? h, string? c, string? r, string? b) EnumerateStreams(JsonElement streams, string type)
    {
        if (streams.ValueKind != JsonValueKind.Array)
            return default;

        foreach (var stream in streams.EnumerateArray())
        {
            if (stream.TryGetProperty("codec_type", out var ct) &&
                ct.ValueKind == JsonValueKind.String &&
                string.Equals(ct.GetString(), type, StringComparison.OrdinalIgnoreCase))
            {
                int? w = null, h = null;
                string? c = null, r = null, b = null;

                if (stream.TryGetProperty("width", out var wEl) && wEl.TryGetInt32(out var wVal))
                    w = wVal;
                if (stream.TryGetProperty("height", out var hEl) && hEl.TryGetInt32(out var hVal))
                    h = hVal;
                if (stream.TryGetProperty("codec_name", out var cn) && cn.ValueKind == JsonValueKind.String)
                    c = cn.GetString();
                if (stream.TryGetProperty("r_frame_rate", out var rf) && rf.ValueKind == JsonValueKind.String)
                    r = rf.GetString();
                if (stream.TryGetProperty("bit_rate", out var br) && br.ValueKind == JsonValueKind.String)
                    b = br.GetString();

                return (w, h, c, r, b);
            }
        }

        return default;
    }

    private static int CountStreams(JsonElement streams, string type)
    {
        if (streams.ValueKind != JsonValueKind.Array)
            return 0;
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
        if (string.IsNullOrWhiteSpace(rFrameRate))
            return 0;
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
