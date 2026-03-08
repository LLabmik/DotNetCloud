namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for file upload behaviour: size limits and temporary file storage.
/// </summary>
public sealed class FileUploadOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "FileUpload";

    /// <summary>
    /// Maximum permitted total file size for a single upload, in bytes.
    /// Default: 15 GB (16,106,127,360 bytes).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 16_106_127_360L;

    /// <summary>
    /// Directory used for temporary file assembly during downloads.
    /// Set programmatically at startup from <c>DOTNETCLOUD_DATA_DIR</c>.
    /// Falls back to <see cref="Path.GetTempPath"/> when not set.
    /// </summary>
    public string? TmpPath { get; set; }
}
