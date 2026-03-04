namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// View model for displaying a file or folder node in the file browser UI.
/// </summary>
public sealed class FileNodeViewModel
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether this is a "File" or "Folder".</summary>
    public string NodeType { get; init; } = "File";

    /// <summary>MIME type (null for folders).</summary>
    public string? MimeType { get; init; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Whether this node is favorited.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Last modified timestamp.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Breadcrumb item for folder navigation path display.
/// </summary>
public sealed record BreadcrumbItem(Guid Id, string Name);

/// <summary>
/// View mode for the file browser (grid or list).
/// </summary>
public enum ViewMode
{
    /// <summary>Grid layout with icons.</summary>
    Grid,

    /// <summary>List layout with details.</summary>
    List
}

/// <summary>
/// View model for a trashed file or folder in the trash bin UI.
/// </summary>
public sealed class TrashItemViewModel
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether this is a "File" or "Folder".</summary>
    public string NodeType { get; init; } = "File";

    /// <summary>Size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>When the item was deleted.</summary>
    public DateTime? DeletedAt { get; init; }
}

/// <summary>
/// View model for tracking an individual file upload.
/// </summary>
public sealed class UploadFileItem
{
    /// <summary>File name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>MIME content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Reference to the browser file for upload.</summary>
    public Microsoft.AspNetCore.Components.Forms.IBrowserFile? BrowserFile { get; init; }

    /// <summary>Upload status.</summary>
    public UploadStatus Status { get; set; } = UploadStatus.Pending;

    /// <summary>Upload progress percentage (0-100).</summary>
    public int Progress { get; set; }
}

/// <summary>
/// View model for displaying the current user's storage quota in the file browser.
/// </summary>
public sealed class QuotaViewModel
{
    /// <summary>Used storage in bytes.</summary>
    public long UsedBytes { get; init; }

    /// <summary>Maximum storage in bytes (0 = unlimited).</summary>
    public long MaxBytes { get; init; }

    /// <summary>Usage percentage (0.0 – 100.0+).</summary>
    public double UsagePercent { get; init; }

    /// <summary>Whether the quota limit has been reached or exceeded.</summary>
    public bool IsExceeded => MaxBytes > 0 && UsedBytes >= MaxBytes;

    /// <summary>Whether usage is in the critical range (>= 95%).</summary>
    public bool IsCritical => MaxBytes > 0 && UsagePercent >= 95.0;

    /// <summary>Whether usage is in the warning range (>= 80%).</summary>
    public bool IsWarning => MaxBytes > 0 && UsagePercent >= 80.0;
}

/// <summary>
/// Status of a file upload operation.
/// </summary>
public enum UploadStatus
{
    /// <summary>Waiting to start.</summary>
    Pending,

    /// <summary>Currently uploading.</summary>
    Uploading,

    /// <summary>Upload finished successfully.</summary>
    Complete,

    /// <summary>Upload failed.</summary>
    Failed
}

/// <summary>
/// View model for a single file version in the version history panel.
/// </summary>
public sealed class FileVersionViewModel
{
    /// <summary>Unique version identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Sequential version number (1 = first version).</summary>
    public int VersionNumber { get; init; }

    /// <summary>Optional user-defined label for this version.</summary>
    public string? Label { get; set; }

    /// <summary>When this version was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Display name of the author who created this version.</summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>Size of this version in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Whether this is the current active version.</summary>
    public bool IsCurrent { get; init; }
}

/// <summary>
/// Navigation sections available in the file browser sidebar.
/// </summary>
public enum FileSidebarSection
{
    /// <summary>Root file listing for the current user.</summary>
    AllFiles,

    /// <summary>Favorited files and folders.</summary>
    Favorites,

    /// <summary>Recently accessed files.</summary>
    Recent,

    /// <summary>Files shared with the current user.</summary>
    SharedWithMe,

    /// <summary>Files shared by the current user.</summary>
    SharedByMe,

    /// <summary>Files grouped by tag.</summary>
    Tags,

    /// <summary>Trash bin.</summary>
    Trash
}

/// <summary>
/// View model for a file tag shown in the sidebar tag list.
/// </summary>
public sealed class FileTagViewModel
{
    /// <summary>Unique tag identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Tag display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional CSS-compatible colour string (e.g. "#e74c3c").</summary>
    public string? Color { get; init; }

    /// <summary>Number of files carrying this tag.</summary>
    public int FileCount { get; init; }
}

/// <summary>
/// View model for the Files module admin settings page.
/// </summary>
public sealed class AdminSettingsViewModel
{
    /// <summary>Default storage quota for new users in gigabytes (0 = unlimited).</summary>
    public double DefaultQuotaGb { get; set; } = 10.0;

    /// <summary>Number of days before trashed files are permanently deleted.</summary>
    public int TrashRetentionDays { get; set; } = 30;

    /// <summary>Maximum number of versions to keep per file (0 = unlimited).</summary>
    public int MaxVersionsPerFile { get; set; } = 50;

    /// <summary>Number of days to retain old versions (0 = unlimited).</summary>
    public int VersionRetentionDays { get; set; } = 365;

    /// <summary>Maximum file upload size in megabytes.</summary>
    public int MaxUploadMb { get; set; } = 100;

    /// <summary>
    /// Comma-separated list of allowed file extensions (e.g. "pdf,docx,png").
    /// Empty means all extensions are allowed.
    /// </summary>
    public string AllowedExtensions { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of blocked file extensions (e.g. "exe,bat,sh").
    /// </summary>
    public string BlockedExtensions { get; set; } = string.Empty;

    /// <summary>Absolute path to the storage root directory on the server.</summary>
    public string StoragePath { get; set; } = string.Empty;
}
