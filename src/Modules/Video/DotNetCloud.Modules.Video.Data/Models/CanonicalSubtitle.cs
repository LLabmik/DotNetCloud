namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical subtitle — subtitles are intrinsic to the video file.
/// No OwnerId — shared across all users.
/// </summary>
public sealed class CanonicalSubtitle
{
    /// <summary>Unique identifier for this subtitle.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The canonical video content hash this subtitle belongs to.</summary>
    public required string VideoContentHash { get; set; }

    /// <summary>Language code (e.g. "en", "fr", "es").</summary>
    public required string Language { get; set; }

    /// <summary>Optional label (e.g. "English (SDH)", "Forced").</summary>
    public string? Label { get; set; }

    /// <summary>Format: "srt" or "vtt".</summary>
    public required string Format { get; set; }

    /// <summary>Subtitle file content.</summary>
    public required string Content { get; set; }

    /// <summary>Whether this is the default subtitle track.</summary>
    public bool IsDefault { get; set; }

    /// <summary>When the subtitle was uploaded (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical video.</summary>
    public CanonicalVideo? Video { get; set; }
}
