namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical video metadata — resolution, codec, bitrate extracted via ffprobe.
/// Stored once per video ContentHash — shared across all users.
/// </summary>
public sealed class CanonicalVideoMetadata
{
    /// <summary>The canonical video content hash (FK to CanonicalVideo).</summary>
    public required string VideoContentHash { get; set; }

    /// <summary>Video width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Video height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Frame rate (frames per second).</summary>
    public double FrameRate { get; set; }

    /// <summary>Video codec name (e.g. "h264", "hevc", "vp9").</summary>
    public string? VideoCodec { get; set; }

    /// <summary>Audio codec name (e.g. "aac", "opus", "ac3").</summary>
    public string? AudioCodec { get; set; }

    /// <summary>Video bitrate in bps.</summary>
    public long Bitrate { get; set; }

    /// <summary>Number of audio tracks.</summary>
    public int AudioTrackCount { get; set; }

    /// <summary>Number of subtitle tracks embedded in the file.</summary>
    public int SubtitleTrackCount { get; set; }

    /// <summary>Container format (e.g. "mp4", "mkv", "webm").</summary>
    public string? ContainerFormat { get; set; }

    /// <summary>When metadata was extracted (UTC).</summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical video.</summary>
    public CanonicalVideo? Video { get; set; }
}
