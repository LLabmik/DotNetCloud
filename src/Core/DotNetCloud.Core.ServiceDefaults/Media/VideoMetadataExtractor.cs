using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.ServiceDefaults.Media;

/// <summary>
/// Extracts metadata from video files using FFprobe (part of the FFmpeg suite).
/// Reads duration, resolution, codec, bitrate, audio tracks, and subtitle tracks.
/// </summary>
/// <remarks>
/// FFprobe must be installed and accessible via the configured path or on <c>PATH</c>.
/// The path can be configured via <c>Media:FfprobePath</c> in application configuration.
/// </remarks>
public sealed class VideoMetadataExtractor : IMediaMetadataExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/mpeg",
        "video/quicktime",
        "video/x-msvideo",
        "video/x-matroska",
        "video/webm",
        "video/x-flv",
        "video/3gpp",
        "video/ogg"
    };

    private readonly string _ffprobePath;
    private readonly ILogger<VideoMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMetadataExtractor"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for FFprobe path.</param>
    /// <param name="logger">Logger instance.</param>
    public VideoMetadataExtractor(IConfiguration configuration, ILogger<VideoMetadataExtractor> logger)
    {
        _ffprobePath = configuration["Media:FfprobePath"] ?? "ffprobe";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public MediaType SupportedMediaType => MediaType.Video;

    /// <inheritdoc />
    public bool CanExtract(string mimeType) => SupportedMimeTypes.Contains(mimeType);

    /// <inheritdoc />
    public async Task<MediaMetadataDto?> ExtractAsync(
        string filePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Video file not found for metadata extraction: {FilePath}", filePath);
            return null;
        }

        try
        {
            var json = await RunFfprobeAsync(filePath, cancellationToken);
            if (json is null) return null;

            return ParseFfprobeOutput(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract video metadata from {FilePath}.", filePath);
            return null;
        }
    }

    private async Task<string?> RunFfprobeAsync(string filePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(filePath);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            _logger.LogWarning("Unable to start ffprobe process for video metadata extraction.");
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            _logger.LogWarning("ffprobe exited with code {ExitCode}. stderr: {StdErr}", process.ExitCode, stderr);
            return null;
        }

        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    private MediaMetadataDto? ParseFfprobeOutput(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int? width = null, height = null;
        double? frameRate = null;
        string? videoCodec = null;
        long? bitrate = null;
        TimeSpan? duration = null;
        int audioTrackCount = 0;
        int subtitleTrackCount = 0;
        int? sampleRate = null;
        int? channels = null;

        // Parse format section
        if (root.TryGetProperty("format", out var format))
        {
            if (format.TryGetProperty("duration", out var durationEl) &&
                double.TryParse(durationEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSec))
            {
                duration = TimeSpan.FromSeconds(durationSec);
            }

            if (format.TryGetProperty("bit_rate", out var bitrateEl) &&
                long.TryParse(bitrateEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var br))
            {
                bitrate = br;
            }
        }

        // Parse streams
        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;

                switch (codecType)
                {
                    case "video":
                        if (width is null) // Take the first video stream
                        {
                            width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : null;
                            height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : null;
                            videoCodec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;

                            if (stream.TryGetProperty("r_frame_rate", out var fr))
                            {
                                frameRate = ParseFrameRate(fr.GetString());
                            }
                        }

                        break;

                    case "audio":
                        audioTrackCount++;
                        if (sampleRate is null)
                        {
                            if (stream.TryGetProperty("sample_rate", out var sr) &&
                                int.TryParse(sr.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var srVal))
                            {
                                sampleRate = srVal;
                            }

                            if (stream.TryGetProperty("channels", out var ch))
                            {
                                channels = ch.GetInt32();
                            }
                        }

                        break;

                    case "subtitle":
                        subtitleTrackCount++;
                        break;
                }
            }
        }

        return new MediaMetadataDto
        {
            MediaType = MediaType.Video,
            Width = width,
            Height = height,
            Duration = duration,
            Codec = videoCodec,
            Bitrate = bitrate,
            SampleRate = sampleRate,
            Channels = channels,
            FrameRate = frameRate,
            AudioTrackCount = audioTrackCount > 0 ? audioTrackCount : null,
            SubtitleTrackCount = subtitleTrackCount > 0 ? subtitleTrackCount : null
        };
    }

    private static double? ParseFrameRate(string? frameRateStr)
    {
        if (string.IsNullOrWhiteSpace(frameRateStr)) return null;

        var parts = frameRateStr.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
            den > 0)
        {
            return Math.Round(num / den, 3);
        }

        if (double.TryParse(frameRateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps))
        {
            return fps;
        }

        return null;
    }
}
