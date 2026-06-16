namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Browser compatibility matrix for video codecs and containers.
/// Determines whether a source video can be direct-played, stream-copied, or must be transcoded.
/// Inspired by Jellyfin's EncodingHelper.CanStreamCopyVideo / CanStreamCopyAudio.
/// Thread-safe (all methods are stateless).
/// </summary>
public sealed class StreamCompatibilityMatrix
{
    /// <summary>
    /// Video codecs that can be direct-played in all modern browsers (H.264 baseline).
    /// </summary>
    private static readonly HashSet<string> UniversalVideoCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264", "avc1", "avc"
    };

    /// <summary>
    /// Video codecs with broad but not universal browser support (HEVC, VP8, VP9, AV1).
    /// </summary>
    private static readonly HashSet<string> BroadVideoCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "hevc", "h265", "hvc1", "hev1",
        "vp8", "vpx",
        "vp9",
        "av1"
    };

    /// <summary>
    /// Audio codecs that all modern browsers can decode.
    /// </summary>
    private static readonly HashSet<string> UniversalAudioCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac", "mp4a"
    };

    /// <summary>
    /// Audio codecs with partial browser support (Edge supports AC3/EAC3; Chrome/Firefox do not).
    /// </summary>
    private static readonly HashSet<string> BroadAudioCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "ac3", "eac3", "opus", "vorbis", "flac"
    };

    /// <summary>
    /// Containers that can be served directly to browsers for direct play.
    /// </summary>
    private static readonly HashSet<string> DirectPlayContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mov", "webm"
    };

    /// <summary>
    /// Containers that can be remuxed (stream-copied) into a browser-compatible container.
    /// These containers are not natively supported by most browsers but can be rewrapped
    /// without re-encoding when the internal codecs are compatible.
    /// </summary>
    private static readonly HashSet<string> RemuxableContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "mkv", "matroska",
        "avi", "avi ",
        "flv",
        "m4v",
        "3gp", "3gpp", "3gpp2",
        "asf", "wmv",
        "mpegts", "mts", "m2ts",
        "ogg", "ogv"
    };

    /// <summary>
    /// Determines the optimal streaming strategy for a video given its codec and container info.
    /// </summary>
    /// <param name="mimeType">MIME type from the database (e.g. "video/mp4", "video/x-matroska").</param>
    /// <param name="videoCodec">Video codec name from ffprobe (e.g. "h264", "hevc", "vp9").</param>
    /// <param name="audioCodec">Audio codec name from ffprobe (e.g. "aac", "ac3", "dts").</param>
    /// <param name="container">Container format from ffprobe (e.g. "mp4", "matroska", "avi").</param>
    /// <returns>The recommended streaming strategy.</returns>
    public StreamingStrategy DecideStrategy(
        string? mimeType,
        string? videoCodec,
        string? audioCodec,
        string? container)
    {
        // If we have no codec info at all, we can't make an informed decision — transcode to be safe
        if (string.IsNullOrEmpty(videoCodec) && string.IsNullOrEmpty(audioCodec))
            return StreamingStrategy.Transcode;

        // Check if the container is directly playable
        bool isDirectPlayContainer = container is not null
            && DirectPlayContainers.Contains(container);

        // Check if the container is remuxable
        bool isRemuxableContainer = container is not null
            && RemuxableContainers.Contains(container);

        // Check video codec compatibility
        bool videoIsUniversal = videoCodec is not null
            && UniversalVideoCodecs.Any(uc => videoCodec.Contains(uc, StringComparison.OrdinalIgnoreCase));
        bool videoIsBroad = videoCodec is not null
            && BroadVideoCodecs.Any(bc => videoCodec.Contains(bc, StringComparison.OrdinalIgnoreCase));

        // Check audio codec compatibility
        bool audioIsUniversal = audioCodec is null
            || audioCodec.Length == 0
            || UniversalAudioCodecs.Any(uc => audioCodec.Contains(uc, StringComparison.OrdinalIgnoreCase));
        bool audioIsBroad = audioCodec is not null
            && BroadAudioCodecs.Any(bc => audioCodec.Contains(bc, StringComparison.OrdinalIgnoreCase));

        // Direct Play: universal codecs in a direct-play container (e.g., H.264+AAC in MP4)
        if (videoIsUniversal && audioIsUniversal && isDirectPlayContainer)
            return StreamingStrategy.DirectPlay;

        // Direct Play with broad codecs: HEVC/VP9/AV1 in WebM or MP4 (browser-dependent but generally works)
        if (videoIsBroad && audioIsUniversal && isDirectPlayContainer)
            return StreamingStrategy.DirectPlay;

        // Stream Copy (remux): universal or broad video + universal audio in a remuxable container
        if ((videoIsUniversal || videoIsBroad) && (audioIsUniversal || audioIsBroad) && isRemuxableContainer)
            return StreamingStrategy.StreamCopy;

        // Stream Copy: universal video codec in any container, even unknown ones
        // (ffmpeg can often remux unknown containers)
        if (videoIsUniversal && audioIsUniversal && container is not null)
            return StreamingStrategy.StreamCopy;

        // Stream Copy: video-only with broad codec (no audio or universal audio) — try remux
        if ((videoIsUniversal || videoIsBroad) && audioIsBroad && isRemuxableContainer)
            return StreamingStrategy.StreamCopy;

        // Stream Copy: broad codecs in any remuxable container
        if (videoIsBroad && isRemuxableContainer)
            return StreamingStrategy.StreamCopy;

        // Everything else: must transcode
        return StreamingStrategy.Transcode;
    }

    /// <summary>
    /// Returns true if the video codec is universally browser-compatible (H.264/AVC).
    /// </summary>
    public static bool IsUniversalVideoCodec(string? codec)
    {
        return codec is not null
            && UniversalVideoCodecs.Any(uc => codec.Contains(uc, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if the video codec has broad browser support (HEVC, VP8, VP9, AV1).
    /// </summary>
    public static bool IsBroadVideoCodec(string? codec)
    {
        return codec is not null
            && BroadVideoCodecs.Any(bc => codec.Contains(bc, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if the audio codec is universally browser-compatible (AAC, MP3).
    /// </summary>
    public static bool IsUniversalAudioCodec(string? codec)
    {
        return codec is null
            || codec.Length == 0
            || UniversalAudioCodecs.Any(uc => codec.Contains(uc, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns true if the audio codec can be stream-copied (browser or player dependent).
    /// </summary>
    public static bool IsCopyableAudioCodec(string? codec)
    {
        return IsUniversalAudioCodec(codec)
            || (codec is not null
                && BroadAudioCodecs.Any(bc => codec.Contains(bc, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns true if the container can be direct-played in browsers.
    /// </summary>
    public static bool IsDirectPlayContainer(string? container)
    {
        return container is not null && DirectPlayContainers.Contains(container);
    }
}
