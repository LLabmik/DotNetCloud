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
    /// Maximum size of a generated multi-item ZIP download, in bytes.
    /// Default: 4 GiB (4,294,967,296 bytes). Downloads exceeding this fail with a 413.
    /// </summary>
    public long MaxZipSizeBytes { get; set; } = 4_294_967_296L;

    /// <summary>
    /// Directory used for temporary file assembly during downloads.
    /// Set programmatically at startup from <c>DOTNETCLOUD_DATA_DIR</c>.
    /// Falls back to <see cref="Path.GetTempPath"/> when not set.
    /// </summary>
    public string? TmpPath { get; set; }
}
