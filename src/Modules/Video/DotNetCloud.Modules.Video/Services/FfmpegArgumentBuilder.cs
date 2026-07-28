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
    private static readonly StreamCompatibilityMatrix _compat = new();

    /// <summary>
    /// Returns the recommended streaming strategy for a video given its codec and container info.
    /// Delegates to <see cref="StreamCompatibilityMatrix"/> for the full browser compatibility matrix.
    /// Supersedes the old CanDirectPlay() method.
    /// </summary>
    public StreamingStrategy DecideStrategy(
        string? mimeType,
        string? videoCodec,
        string? audioCodec,
        string? container)
    {
        return _compat.DecideStrategy(mimeType, videoCodec, audioCodec, container);
    }

    /// <summary>
    /// Returns true if the video can be played directly in HTML5 browsers
    /// without transcoding. Legacy method — prefer <see cref="DecideStrategy"/>.
    /// Must be H.264 or AVC1 video + AAC/MP3/no audio + MP4 container + video/mp4 MIME type.
    /// HEVC, VP9, WebM, and all other codecs/containers will return false.
    /// </summary>
    public bool CanDirectPlay(string mimeType, string? videoCodec, string? audioCodec, string container)
    {
        // Only support MP4 container with video/mp4 MIME type
        if (!string.Equals(mimeType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(container, "mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // Only support H264 and AVC1 video codecs
        if (videoCodec is null)
            return false;
        if (!string.Equals(videoCodec, "h264", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(videoCodec, "avc1", StringComparison.OrdinalIgnoreCase))
            return false;

        // Support AAC, MP3, or no audio
        if (!string.IsNullOrEmpty(audioCodec) &&
            !string.Equals(audioCodec, "aac", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(audioCodec, "mp3", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Builds ffmpeg arguments for stream copy (remux) — changes the container without re-encoding.
    /// Near-instant and lossless. Use when source codecs are browser-compatible but the
    /// container is not (e.g., H.264+AAC in MKV → MP4).
    /// Output goes to stdout (pipe:1) for direct HTTP response streaming.
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="videoCodec">Source video codec (e.g. "h264"). Used to decide bitstream filter.</param>
    /// <param name="audioCodec">Source audio codec (e.g. "aac"). If not browser-compatible, audio is re-encoded.</param>
    /// <param name="outputContainer">Target container: "mp4" or "webm". MP4 is the default.</param>
    /// <param name="startTime">Optional seek position in the source file (applied via -ss before -i).</param>
    /// <returns>Full ffmpeg argument string (does NOT include the "ffmpeg" binary name).</returns>
    public string GetStreamCopyArgs(
        string inputPath,
        string? videoCodec,
        string? audioCodec,
        string outputContainer = "mp4",
        TimeSpan? startTime = null)
    {
        var sb = new StringBuilder();
        sb.Append("-nostdin -hide_banner -loglevel warning ");
        sb.Append("-fflags +genpts ");  // Generate PTS if missing (common in MKV/AVI)
        if (startTime.HasValue && startTime.Value > TimeSpan.Zero)
        {
            // Fast keyframe seek BEFORE -i (imprecise for video, precise for audio).
            // The avoid_negative_ts flag below re-bases timestamps so output starts at 0.
            sb.AppendFormat(CultureInfo.InvariantCulture, "-ss {0:F3} ", startTime.Value.TotalSeconds);
        }
        sb.AppendFormat(CultureInfo.InvariantCulture, "-i \"{0}\" ", EscapePath(inputPath));

        // Re-base timestamps to start at 0 and ensure strict A/V interleaving.
        // Without these, fast -ss before -i can cause audio/video to start at
        // different positions because video seeks to a keyframe while audio seeks
        // precisely — producing a persistent offset.
        sb.Append("-avoid_negative_ts make_zero -max_interleave_delta 0 ");

        // Map streams
        sb.Append("-map 0:v:0? -map 0:a:0? ");

        // Video: stream copy
        sb.Append("-c:v copy ");

        // Bitstream filter for H.264 in MPEG-TS → MP4: convert annex-b to avcC
        if (videoCodec is not null
            && (videoCodec.Contains("h264", StringComparison.OrdinalIgnoreCase)
                || videoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append("-bsf:v h264_mp4toannexb=disable ");
        }

        // Bitstream filter for H.265/HEVC
        if (videoCodec is not null
            && (videoCodec.Contains("hevc", StringComparison.OrdinalIgnoreCase)
                || videoCodec.Contains("h265", StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append("-bsf:v hevc_mp4toannexb=disable ");
        }

        // Audio: stream copy if compatible, otherwise transcode to AAC
        if (audioCodec is not null && StreamCompatibilityMatrix.IsUniversalAudioCodec(audioCodec))
        {
            sb.Append("-c:a copy ");
        }
        else
        {
            sb.Append("-strict -2 -c:a aac -b:a 128k -ac 2 ");
        }

        // Remove metadata (cleaner output)
        sb.Append("-map_metadata -1 ");

        // Faststart for web streaming + fragmented for progressive download
        sb.Append("-movflags +faststart+frag_keyframe+empty_moov ");

        // Output format
        sb.AppendFormat(CultureInfo.InvariantCulture, "-f {0} ", outputContainer);

        // Overwrite
        sb.Append("-y ");

        // Output to stdout (pipe to HTTP response)
        sb.Append("-frag_duration 10000000 "); // ~10ms fragment duration for low-latency streaming
        sb.Append("pipe:1");

        return sb.ToString();
    }

    /// <summary>
    /// Builds ffmpeg arguments for stream copy that writes to a file (not stdout).
    /// Used when the output file is needed for subsequent serving (e.g., direct play from remuxed file).
    /// </summary>
    public string GetStreamCopyToFileArgs(
        string inputPath,
        string outputPath,
        string? videoCodec,
        string? audioCodec,
        string outputContainer = "mp4")
    {
        var sb = new StringBuilder();
        sb.Append("-nostdin -hide_banner -loglevel warning ");
        sb.Append("-fflags +genpts ");
        sb.AppendFormat(CultureInfo.InvariantCulture, "-i \"{0}\" ", EscapePath(inputPath));
        sb.Append("-map 0:v:0? -map 0:a:0? ");
        sb.Append("-c:v copy ");

        if (videoCodec is not null
            && (videoCodec.Contains("h264", StringComparison.OrdinalIgnoreCase)
                || videoCodec.Contains("avc", StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append("-bsf:v h264_mp4toannexb=disable ");
        }

        if (videoCodec is not null
            && (videoCodec.Contains("hevc", StringComparison.OrdinalIgnoreCase)
                || videoCodec.Contains("h265", StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append("-bsf:v hevc_mp4toannexb=disable ");
        }

        if (audioCodec is not null && StreamCompatibilityMatrix.IsUniversalAudioCodec(audioCodec))
        {
            sb.Append("-c:a copy ");
        }
        else
        {
            sb.Append("-strict -2 -c:a aac -b:a 128k -ac 2 ");
        }

        sb.Append("-map_metadata -1 ");
        sb.Append("-movflags +faststart ");
        sb.AppendFormat(CultureInfo.InvariantCulture, "-f {0} ", outputContainer);
        sb.Append("-y ");
        sb.AppendFormat(CultureInfo.InvariantCulture, "\"{0}\"", EscapePath(outputPath));

        return sb.ToString();
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
        // -nostdin: prevents ffmpeg from reading stdin for keyboard shortcuts.
        // In a systemd service, stdin is /dev/null — reading it returns EOF immediately,
        // which causes some ffmpeg builds to quit after only a few segments.
        sb.Append("-nostdin -hide_banner -loglevel warning ");

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
    ///
    /// This is the universal fallback — works in all browsers via hls.js or native HLS.
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
        return BuildHlsArgs(inputPath, outputDir, options, sourceVideoCodec: null, sourceAudioCodec: null, seekStart, seekDuration);
    }

    /// <summary>
    /// Builds HLS transcoding arguments with source-codec-aware optimization.
    /// When audio is browser-compatible but video isn't, only the video is re-encoded
    /// and audio is stream-copied (saves CPU and preserves quality).
    /// </summary>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputDir">Absolute path to the directory where segments and playlist are written.</param>
    /// <param name="options">Transcoding options (codec, CRF, preset, bitrate, etc.).</param>
    /// <param name="sourceVideoCodec">Source video codec from ffprobe (e.g. "h264"). Only used for bitstream filter.</param>
    /// <param name="sourceAudioCodec">Source audio codec from ffprobe (e.g. "aac", "ac3"). If browser-compatible, audio is stream-copied instead of re-encoded.</param>
    /// <param name="seekStart">Optional start time for seeking.</param>
    /// <param name="seekDuration">Optional duration to transcode (TimeSpan or null = full file).</param>
    /// <returns>Full ffmpeg argument string (does NOT include the "ffmpeg" binary name).</returns>
    public string BuildHlsArgs(
        string inputPath,
        string outputDir,
        VideoTranscodingOptions options,
        string? sourceVideoCodec,
        string? sourceAudioCodec,
        TimeSpan? seekStart = null,
        TimeSpan? seekDuration = null)
    {
        var sb = new StringBuilder();

        // --- Hide banner and set log level ---
        // -nostdin: prevents ffmpeg from reading stdin for keyboard shortcuts.
        // In a systemd service, stdin is /dev/null — reading it returns EOF immediately,
        // which causes some ffmpeg builds to quit after only a few segments.
        sb.Append("-nostdin -hide_banner -loglevel warning ");

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

        // --- Map streams ---
        sb.Append("-map 0:v:0? -map 0:a:0? ");

        // --- Jellyfin-style timestamp handling: preserve source timestamps ---
        sb.Append("-copyts -avoid_negative_ts disabled ");

        // --- Video codec + preset + quality (Jellyfin-style: no forced profile) ---
        sb.AppendFormat(CultureInfo.InvariantCulture, "-c:v:0 {0} ", options.VideoCodec);
        if (!string.IsNullOrEmpty(options.EncoderPreset))
            sb.AppendFormat(CultureInfo.InvariantCulture, "-preset {0} ", options.EncoderPreset);
        sb.AppendFormat(CultureInfo.InvariantCulture, "-crf {0} ", options.VideoCrf);
        sb.Append("-pix_fmt yuv420p ");
        sb.Append("-sc_threshold:v:0 0 ");

        // --- Resolution scaling ---
        if (options.MaxWidth > 0 && options.MaxHeight > 0)
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "-vf \"scale='min({0},iw)':'min({1},ih)':force_original_aspect_ratio=decrease,pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ",
                options.MaxWidth, options.MaxHeight);
        else
            sb.Append("-vf \"pad='ceil(iw/2)*2':'ceil(ih/2)*2'\" ");

        // --- Keyframe alignment for HLS segments (Jellyfin-style) ---
        sb.Append("-g:v:0 150 -keyint_min:v:0 150 ");
        sb.Append("-force_key_frames:0 \"expr:gte(t,n_forced*6)\" ");

        // --- Audio codec + settings (smart: copy if compatible, transcode otherwise) ---
        var shouldCopyAudio = sourceAudioCodec is not null
            && StreamCompatibilityMatrix.IsCopyableAudioCodec(sourceAudioCodec);

        if (shouldCopyAudio)
        {
            sb.Append("-c:a:0 copy ");
        }
        else
        {
            // -strict -2 MUST come before codec selection to enable AAC encoder
            sb.Append("-strict -2 ");
            sb.AppendFormat(CultureInfo.InvariantCulture, "-c:a:0 {0} ", MapAudioCodec(sourceAudioCodec, options.AudioCodec));
            sb.AppendFormat(CultureInfo.InvariantCulture, "-b:a {0}k ", options.AudioBitrateKbps);
            sb.Append("-ac 2 ");
        }

        // --- Remove metadata ---
        sb.Append("-map_metadata -1 -map_chapters -1 ");

        // --- HLS output (Jellyfin-style: mpegts, vod, no global_header) ---
        sb.Append("-f hls ");
        sb.Append("-max_delay 5000000 ");
        sb.Append("-hls_playlist_type event ");
        sb.Append("-hls_time 6 ");
        sb.Append("-hls_list_size 0 ");
        sb.Append("-start_number 0 ");
        sb.Append("-hls_segment_type mpegts ");

        // --- Segment filenames (absolute path, safer than relative) ---
        var escapedDir = EscapePath(outputDir);
        sb.AppendFormat(CultureInfo.InvariantCulture, "-hls_segment_filename \"{0}/segment_%05d.ts\" ", escapedDir);

        // --- Overwrite ---
        sb.Append("-y ");

        // --- Playlist output file (absolute path) ---
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
    /// Maps a source audio codec to the best ffmpeg output codec for web playback.
    /// Preserves codec when browser-compatible, falls back to AAC otherwise.
    /// </summary>
    /// <param name="sourceCodec">Source audio codec from ffprobe (e.g. "ac3", "dts", "flac").</param>
    /// <param name="defaultCodec">The configured default audio codec (from VideoTranscodingOptions).</param>
    /// <returns>ffmpeg codec name (e.g. "aac", "libmp3lame", "copy").</returns>
    public static string MapAudioCodec(string? sourceCodec, string defaultCodec)
    {
        if (sourceCodec is null)
            return defaultCodec;

        // AC3 / E-AC3 → copy if supported (Edge), otherwise no browser supports → transcode
        if (sourceCodec.Contains("ac3", StringComparison.OrdinalIgnoreCase) ||
            sourceCodec.Contains("eac3", StringComparison.OrdinalIgnoreCase))
            return "aac";

        // DTS, TrueHD, DTS-HD → no browser supports → transcode to AAC
        if (sourceCodec.Contains("dts", StringComparison.OrdinalIgnoreCase) ||
            sourceCodec.Contains("truehd", StringComparison.OrdinalIgnoreCase))
            return "aac";

        // FLAC → copy (Chrome, Firefox, Edge support it)
        if (sourceCodec.Contains("flac", StringComparison.OrdinalIgnoreCase))
            return "copy";

        // Opus → copy in WebM container (Chrome, Firefox support it)
        if (sourceCodec.Contains("opus", StringComparison.OrdinalIgnoreCase))
            return "copy";

        // Vorbis → copy in WebM/OGG (Chrome, Firefox support it)
        if (sourceCodec.Contains("vorbis", StringComparison.OrdinalIgnoreCase))
            return "copy";

        // MP2 / MP3 → transcode to AAC (MP3 in MPEG-TS not supported by Chrome MSE)
        if (sourceCodec.Contains("mp2", StringComparison.OrdinalIgnoreCase) ||
            sourceCodec.Contains("mp3", StringComparison.OrdinalIgnoreCase))
            return "aac";

        // Unknown → use configured default (usually AAC)
        return defaultCodec;
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
