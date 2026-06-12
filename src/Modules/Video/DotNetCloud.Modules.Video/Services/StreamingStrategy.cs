namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Streaming strategy determined by analyzing the source video codecs and browser compatibility.
/// </summary>
public enum StreamingStrategy
{
    /// <summary>
    /// Source video can be served directly to the browser without any ffmpeg processing.
    /// Requires: browser-compatible container (MP4, WebM) + compatible codecs (H.264, VP8/VP9, AV1).
    /// </summary>
    DirectPlay,

    /// <summary>
    /// Source codecs are browser-compatible but the container is not.
    /// Use ffmpeg stream copy (-c copy) to remux into MP4/WebM without re-encoding.
    /// Near-instant (under 3 seconds for most files) and lossless.
    /// </summary>
    StreamCopy,

    /// <summary>
    /// Source codecs are not browser-compatible (or codec is unknown).
    /// Full re-encode to H.264 + AAC via HLS.
    /// Takes minutes, uses significant CPU.
    /// </summary>
    Transcode
}
