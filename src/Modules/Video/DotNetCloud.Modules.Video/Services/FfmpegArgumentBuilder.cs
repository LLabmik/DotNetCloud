using System.Globalization;
using System.Text;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Builds ffmpeg command-line arguments for video transcoding.
/// Inspired by Jellyfin's EncodingHelper.
/// Thread-safe (all methods are stateless).
/// </summary>
public sealed class FfmpegArgumentBuilder
{
    /// <summary>
    /// Returns true if the video can be played directly in HTML5 browsers
    /// without transcoding.
    /// Must be H.264 or H.265 video + AAC or MP3 audio + MP4 container.
    /// </summary>
    public bool CanDirectPlay(string mimeType, string? videoCodec, string? audioCodec, string container)
    {
        // MIME must be video/mp4
        if (!string.Equals(mimeType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // Container must be mp4
        if (!string.Equals(container, "mp4", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(container, "mov", StringComparison.OrdinalIgnoreCase))
            return false;

        // Video codec must be H.264 (avc1) or H.265 (hevc/hvc1)
        // H.265 has partial browser support; H.264 is universal
        bool videoOk = videoCodec is not null && (
            videoCodec.Contains("h264", StringComparison.OrdinalIgnoreCase) ||
            videoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase));

        // Audio must be AAC or MP3
        bool audioOk = audioCodec is null || audioCodec.Length == 0 || (
            audioCodec.Contains("aac", StringComparison.OrdinalIgnoreCase) ||
            audioCodec.Contains("mp3", StringComparison.OrdinalIgnoreCase));

        return videoOk && audioOk;
    }

    /// <summary>
    /// Builds the ffmpeg command-line arguments for progressive MP4 transcoding.
    /// Output: H.264 + AAC in MP4 with faststart for web streaming.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputPath">Absolute path where the transcoded file will be written.</param>
    /// <param name="options">Transcoding options (codec, CRF, preset, bitrate, etc.).</param>
    /// <param name="seekStart">Optional start time for seeking.</param>
    /// <param name="seekDuration">Optional duration to transcode (TimeSpan or null = full file).</param>
    /// <returns>Full ffmpeg argument string (does NOT include the "ffmpeg" binary name).</returns>
    public string BuildProgressiveMp4Args(
        string inputPath,
        string outputPath,
        VideoTranscodingOptions options,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null)
    {
        var sb = new StringBuilder();

        // --- Hide banner and set log level ---
        sb.Append("-hide_banner -loglevel warning ");

        // --- Thread count ---
        if (options.ThreadCount > 0)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-threads {0} ", options.ThreadCount);
        }

        // --- Seeking (must come before -i) ---
        if (seekStart.HasValue && seekStart.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-ss {0} ", seekStart.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Input file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-i \"{0}\" ", EscapePath(inputPath));

        // --- Duration limit ---
        if (seekDuration.HasValue && seekDuration.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-t {0} ", seekDuration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Map all streams we want ---
        sb.Append("-map 0:v:0? -map 0:a:0? ");

        // --- Video codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:v {0} ", options.VideoCodec);

        // --- Video preset ---
        if (!string.IsNullOrEmpty(options.EncoderPreset))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-preset {0} ", options.EncoderPreset);
        }

        // --- Video CRF (quality) ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-crf {0} ", options.VideoCrf);

        // --- Pixel format (ensure browser compatibility) ---
        sb.Append("-pix_fmt yuv420p ");

        // --- Resolution scaling ---
        if (options.MaxWidth > 0 && options.MaxHeight > 0)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "-vf \"scale='min({0},iw)':'min({1},ih)':force_original_aspect_ratio=decrease,pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ",
                options.MaxWidth,
                options.MaxHeight);
        }
        else
        {
            // Just ensure even dimensions (some codecs require it)
            sb.Append("-vf \"pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ");
        }

        // --- Audio codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:a {0} ", options.AudioCodec);

        // --- Audio bitrate ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-b:a {0}k ", options.AudioBitrateKbps);

        // --- Audio channels (stereo) ---
        sb.Append("-ac 2 ");

        // --- Remove metadata from source ---
        sb.Append("-map_metadata -1 ");

        // --- Faststart for web streaming (moves moov atom to beginning after encoding) ---
        sb.Append("-movflags +faststart ");

        // --- Output format ---
        sb.Append("-f mp4 ");

        // --- Overwrite output ---
        sb.Append("-y ");

        // --- Output file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\"", EscapePath(outputPath));

        return sb.ToString();
    }

    /// <summary>
    /// Builds the ffmpeg command-line arguments for HLS (HTTP Live Streaming) transcoding.
    /// Output: H.264 + AAC segmented into .ts files with an .m3u8 playlist.
    /// ffmpeg writes self-contained 6-second segments that are playable immediately,
    /// so the browser can start playback before transcoding finishes.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputDir">Absolute path to the directory where segments and playlist are written.</param>
    /// <param name="options">Transcoding options (codec, CRF, preset, bitrate, etc.).</param>
    /// <param name="seekStart">Optional start time for seeking.</param>
    /// <param name="seekDuration">Optional duration to transcode (TimeSpan or null = full file).</param>
    /// <returns>Full ffmpeg argument string (does NOT include the "ffmpeg" binary name).</returns>
    public string BuildHlsArgs(
        string inputPath,
        string outputDir,
        VideoTranscodingOptions options,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null)
    {
        var sb = new StringBuilder();

        // --- Hide banner and set log level ---
        sb.Append("-hide_banner -loglevel warning ");

        // --- Thread count ---
        if (options.ThreadCount > 0)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-threads {0} ", options.ThreadCount);
        }

        // --- Seeking (must come before -i) ---
        if (seekStart.HasValue && seekStart.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-ss {0} ", seekStart.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Input file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-i \"{0}\" ", EscapePath(inputPath));

        // --- Duration limit ---
        if (seekDuration.HasValue && seekDuration.Value > TimeSpan.Zero)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-t {0} ", seekDuration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        // --- Map all streams we want ---
        sb.Append("-map 0:v:0? -map 0:a:0? ");

        // --- Video codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:v {0} ", options.VideoCodec);

        // --- Video preset ---
        if (!string.IsNullOrEmpty(options.EncoderPreset))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "-preset {0} ", options.EncoderPreset);
        }

        // --- Video CRF (quality) ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-crf {0} ", options.VideoCrf);

        // --- Pixel format (ensure browser compatibility) ---
        sb.Append("-pix_fmt yuv420p ");

        // --- Resolution scaling ---
        if (options.MaxWidth > 0 && options.MaxHeight > 0)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "-vf \"scale='min({0},iw)':'min({1},ih)':force_original_aspect_ratio=decrease,pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ",
                options.MaxWidth,
                options.MaxHeight);
        }
        else
        {
            // Just ensure even dimensions (some codecs require it)
            sb.Append("-vf \"pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ");
        }

        // --- Audio codec ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:a {0} ", options.AudioCodec);

        // --- Audio bitrate ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-b:a {0}k ", options.AudioBitrateKbps);

        // --- Audio channels (stereo) ---
        sb.Append("-ac 2 ");

        // --- Remove metadata from source ---
        sb.Append("-map_metadata -1 ");

        // --- Force keyframes every 2 seconds (ensures clean segment boundaries) ---
        sb.Append("-force_key_frames \"expr:gte(t,n_forced*2)\" ");

        // --- HLS output format ---
        sb.Append("-f hls ");

        // --- Atomic segment writes: write to .tmp, rename when complete ---
        sb.Append("-hls_flags temp_file ");

        // --- HLS segment duration (6 seconds) ---
        sb.Append("-hls_time 6 ");

        // --- Keep all segments in the playlist (don't limit playlist length) ---
        sb.Append("-hls_list_size 0 ");

        // --- Start playlist numbering at 0 ---
        sb.Append("-start_number 0 ");

        // --- Segment filename pattern ---
        var escapedDir = EscapePath(outputDir);
        sb.AppendFormat(CultureInfo.InvariantCulture, "-hls_segment_filename \"{0}/segment_%05d.ts\" ", escapedDir);

        // --- Overwrite output ---
        sb.Append("-y ");

        // --- Playlist output file ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}/playlist.m3u8\"", escapedDir);

        return sb.ToString();
    }

    /// <summary>
    /// Builds ffprobe arguments to extract stream info as JSON.
    /// </summary>
    public string BuildFfprobeArgs(string inputPath)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "-v quiet -print_format json -show_format -show_streams \"{0}\"",
            EscapePath(inputPath));
    }

    /// <summary>
    /// Escapes a file path for safe use in ffmpeg command lines.
    /// Handles both Windows backslashes and special characters.
    /// </summary>
    private static string EscapePath(string path)
    {
        return path.Replace('\\', '/')
                   .Replace("\"", "\\\"");
    }
}
