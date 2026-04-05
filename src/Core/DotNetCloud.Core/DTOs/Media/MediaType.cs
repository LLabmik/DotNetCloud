namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Classifies the kind of media a file represents.
/// Used by metadata extractors and media modules to determine processing pipelines.
/// </summary>
public enum MediaType
{
    /// <summary>A photograph or raster image (JPEG, PNG, GIF, WebP, BMP, TIFF, etc.).</summary>
    Photo,

    /// <summary>An audio file (MP3, FLAC, OGG, AAC, OPUS, WAV, WMA, etc.).</summary>
    Audio,

    /// <summary>A video file (MP4, MKV, WebM, AVI, MOV, etc.).</summary>
    Video
}
