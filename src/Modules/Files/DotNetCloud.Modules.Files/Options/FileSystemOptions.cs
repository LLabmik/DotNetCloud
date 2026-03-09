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
}
