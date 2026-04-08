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
    }

    /// <inheritdoc />
    public async Task<(Stream? Data, string? ContentType)> GetThumbnailAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var data = await _db.Videos.IgnoreQueryFilters()
            .Where(v => v.Id == videoId)
            .Select(v => v.ThumbnailPoster)
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null || data.Length == 0)
        {
            return (null, null);
        }

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
            var extracted = await ExtractFrameAsync(tempVideoPath, tempFramePath, cancellationToken);
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
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Video thumbnail deleted for {VideoId}", videoId);
    }

    private async Task<bool> ExtractFrameAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
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
            startInfo.ArgumentList.Add("10%"); // 10% into the video (skips intros)
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
