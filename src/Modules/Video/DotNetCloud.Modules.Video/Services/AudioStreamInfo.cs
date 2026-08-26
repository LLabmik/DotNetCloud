namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// An audio stream inside a video container, as reported by ffprobe.
/// <see cref="Index"/> is the 0-based positional index of the stream *within the
/// audio streams* (matching ffmpeg's <c>-map 0:a:N</c> selectors and the order of
/// the streams returned by ffprobe), NOT the absolute stream index from the file.
/// Using the positional index keeps it consistent everywhere it flows: the
/// <c>/streams</c> API response, the <c>audioStreamIndex</c> query parameter, and
/// the <c>-map 0:a:N</c> argument builder.
/// </summary>
public sealed record AudioStreamInfo
{
    /// <summary>0-based positional index of this audio stream (within audio streams).</summary>
    public int Index { get; init; }

    /// <summary>Audio codec name (e.g. "aac", "ac3", "dts").</summary>
    public string? Codec { get; init; }

    /// <summary>ISO 639 language code (e.g. "eng", "jpn"), may be absent.</summary>
    public string? Language { get; init; }

    /// <summary>Optional human-readable track title (e.g. "Stereo", "Commentary").</summary>
    public string? Title { get; init; }

    /// <summary>Number of audio channels (e.g. 2 for stereo, 6 for 5.1).</summary>
    public int? Channels { get; init; }

    /// <summary>Whether this is the default audio stream.</summary>
    public bool IsDefault { get; init; }
}
