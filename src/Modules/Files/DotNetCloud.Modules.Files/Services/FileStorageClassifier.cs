namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Classifies files by whether they are immutable media (photos, music, video) that can be
/// stored as a single whole-file blob instead of content-addressed chunks.
/// </summary>
/// <remarks>
/// Media files are immutable in practice — a re-encode produces an entirely new file rather than an
/// incremental edit — so content-defined chunking buys no delta-upload benefit while costing a full
/// reassembly on every read. Documents (office, PDF, text, archives, unknown) stay chunked.
/// </remarks>
public static class FileStorageClassifier
{
    /// <summary>
    /// File extensions treated as immutable media, used as a fallback when the MIME type is missing.
    /// Mirrors the photo/music/video extension sets used by media discovery and the image helper.
    /// </summary>
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Photos
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff", ".tif",
        ".svg", ".heic", ".heif", ".raw", ".cr2", ".nef", ".arw",
        ".avif", ".ico", ".jfif", ".pjpeg", ".pjp",
        // Music
        ".mp3", ".flac", ".ogg", ".oga", ".opus", ".aac", ".m4a",
        ".wav", ".wma", ".aiff", ".aif", ".wv", ".ape",
        // Video
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".flv",
        ".webm", ".3gp", ".mpg", ".mpeg", ".ts",
    };

    /// <summary>
    /// Returns <see langword="true"/> when the MIME type (preferred) or, failing that, the file name
    /// extension indicates an immutable media file (<c>image/*</c>, <c>audio/*</c>, <c>video/*</c>).
    /// </summary>
    /// <param name="mimeType">MIME type reported by the client, if any.</param>
    /// <param name="fileName">File name, used for extension-based fallback.</param>
    public static bool IsImmutableMedia(string? mimeType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            var trimmed = mimeType.TrimStart();
            if (trimmed.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && MediaExtensions.Contains(extension);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the file is immutable media <b>and</b> the whole-file
    /// storage option is enabled. This is the gate used by the write and read paths.
    /// </summary>
    /// <param name="mimeType">MIME type reported by the client, if any.</param>
    /// <param name="fileName">File name, used for extension-based fallback.</param>
    /// <param name="optionEnabled">Whether <c>FileUploadOptions.WholeFileMediaStorage</c> is enabled.</param>
    public static bool IsWholeFileEligible(string? mimeType, string? fileName, bool optionEnabled)
        => optionEnabled && IsImmutableMedia(mimeType, fileName);
}
