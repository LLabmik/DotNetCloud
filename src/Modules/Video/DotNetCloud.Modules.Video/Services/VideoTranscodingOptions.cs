namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Configuration options for video transcoding.
/// Bound from configuration section "Video:Transcoding".
/// </summary>
public sealed class VideoTranscodingOptions
{
    /// <summary>Path to the ffmpeg binary. Default "ffmpeg" (resolved from PATH).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Maximum number of concurrent ffmpeg transcode processes.</summary>
    public int MaxConcurrentJobs { get; set; } = 2;

    /// <summary>Directory for temporary transcode output files (while in progress).</summary>
    public string TempDirectory { get; set; } = string.Empty;

    /// <summary>How long cached transcode outputs are kept, in hours. 0 = never expire.</summary>
    public int CacheTtlHours { get; set; } = 168; // 7 days

    /// <summary>Maximum total size of the transcode cache in bytes. 0 = unlimited.</summary>
    public long MaxCacheSizeBytes { get; set; } = 25L * 1024 * 1024 * 1024; // 25 GB

    /// <summary>How long (seconds) a running HLS stream may go without any segment or playlist
    /// request before the idle watchdog cancels its ffmpeg process. 0 disables the watchdog.
    /// Default 300 (5 minutes).</summary>
    public int HlsIdleTimeoutSeconds { get; set; } = 300;

    /// <summary>How often (seconds) the HLS idle watchdog scans running streams. Default 20.</summary>
    public int HlsWatchdogIntervalSeconds { get; set; } = 20;

    /// <summary>Video codec: "libx264" (default), "libx265", "libvpx-vp9".</summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>CRF value for video quality. Lower = better. 23 is default for x264.</summary>
    public int VideoCrf { get; set; } = 20;

    /// <summary>Encoder preset: "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow".</summary>
    public string EncoderPreset { get; set; } = "fast";

    /// <summary>Maximum output video width. Source is scaled down if wider. 0 = no limit.</summary>
    public int MaxWidth { get; set; } = 1920;

    /// <summary>Maximum output video height. Source is scaled down if taller. 0 = no limit.</summary>
    public int MaxHeight { get; set; } = 1080;

    /// <summary>Audio codec: "aac" (default), "libmp3lame", "opus".</summary>
    public string AudioCodec { get; set; } = "aac";

    /// <summary>Audio bitrate in kbps. Default 128.</summary>
    public int AudioBitrateKbps { get; set; } = 256;

    /// <summary>ffmpeg thread count. 0 = auto.</summary>
    public int ThreadCount { get; set; } = 0;

    /// <summary>
    /// MIME types that can be direct-played (served as-is without transcoding).
    /// Videos with MIME types NOT in this list are always transcoded.
    /// Video codec and container are also checked via ffprobe.
    /// </summary>
    public HashSet<string> DirectPlayMimeTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4"
    };
}
