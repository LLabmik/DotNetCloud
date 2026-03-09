namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for file-system-level behaviour such as cross-platform compatibility enforcement.
/// </summary>
public sealed class FileSystemOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "FileSystem";

    /// <summary>
    /// When <see langword="true"/> (default), creating or renaming a file or folder whose name
    /// differs only in casing from an existing sibling is rejected with a 409 Conflict response.
    /// This prevents data loss on case-insensitive file systems (Windows, macOS) when a
    /// Linux client creates case-variant filenames.
    /// Disable only if every client in your deployment runs a case-sensitive OS.
    /// </summary>
    public bool EnforceCaseInsensitiveUniqueness { get; set; } = true;

    /// <summary>
    /// Total relative path length (in characters) beyond which a warning header
    /// <c>X-Path-Warning: path-length-exceeds-windows-limit</c> is returned.
    /// Windows clients with long-path support disabled cannot sync paths longer than 260
    /// characters total. Default: 250 (provides headroom for a typical sync-root prefix).
    /// </summary>
    public int MaxPathWarningThreshold { get; set; } = 250;

    /// <summary>
    /// When <see langword="true"/> (default), filenames containing characters that are
    /// illegal on Windows (<c>\ / : * ? " &lt; &gt; |</c> and control characters 0x00–0x1F)
    /// are rejected with <c>400 Bad Request</c>.
    /// Disable only if all clients are on Linux/macOS and interop with Windows is not required.
    /// </summary>
    public bool EnforceWindowsFilenameCompatibility { get; set; } = true;
}
