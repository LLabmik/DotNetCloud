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

    /// <summary>
    /// URL to a server-generated thumbnail image, or <c>null</c> if no thumbnail is available
    /// (e.g. for folders or unsupported file types). Populated from the thumbnail API.
    /// </summary>
    public string? ThumbnailUrl { get; init; }
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

    /// <summary>Current upload throughput in bytes per second (updated during upload).</summary>
    public double SpeedBytesPerSecond { get; set; }

    /// <summary>Estimated seconds remaining for this file (null when unknown).</summary>
    public double? EtaSeconds { get; set; }

    /// <summary>Whether the upload has been paused by the user.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Whether the upload has been cancelled by the user.</summary>
    public bool IsCancelled { get; set; }
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
    Failed,

    /// <summary>Upload paused by the user.</summary>
    Paused
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
/// View model for displaying an existing share on a file or folder.
/// </summary>
public sealed class ShareViewModel
{
    /// <summary>Share ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Share type: "User", "Team", "Group", or "PublicLink".</summary>
    public string ShareType { get; init; } = string.Empty;

    /// <summary>Display name of the share recipient (user name, team name, or "Public Link").</summary>
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>Permission level: "Read", "ReadWrite", or "Full".</summary>
    public string Permission { get; set; } = "Read";

    /// <summary>Public link token (only for PublicLink shares).</summary>
    public string? LinkToken { get; init; }

    /// <summary>Full public link URL (only for PublicLink shares).</summary>
    public string? LinkUrl { get; init; }

    /// <summary>Whether the public link has a password set.</summary>
    public bool HasPassword { get; init; }

    /// <summary>Download count (for public links).</summary>
    public int DownloadCount { get; init; }

    /// <summary>Max downloads (for public links, null = unlimited).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>Expiration date (null = never).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>When the share was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Note attached to the share.</summary>
    public string? Note { get; init; }

    /// <summary>Whether this share is expired.</summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

/// <summary>
/// View model for an item in the "Shared with me" or "Shared by me" views.
/// </summary>
public sealed class SharedItemViewModel
{
    /// <summary>Share ID.</summary>
    public Guid ShareId { get; init; }

    /// <summary>The shared file or folder node ID.</summary>
    public Guid NodeId { get; init; }

    /// <summary>Display name of the file or folder.</summary>
    public string NodeName { get; init; } = string.Empty;

    /// <summary>Whether this is a "File" or "Folder".</summary>
    public string NodeType { get; init; } = "File";

    /// <summary>File size in bytes (0 for folders).</summary>
    public long Size { get; init; }

    /// <summary>MIME type (null for folders).</summary>
    public string? MimeType { get; init; }

    /// <summary>Share type: "User", "Team", "Group", or "PublicLink".</summary>
    public string ShareType { get; init; } = string.Empty;

    /// <summary>Permission level: "Read", "ReadWrite", or "Full".</summary>
    public string Permission { get; init; } = "Read";

    /// <summary>Display name of the person who shared (for "Shared with me").</summary>
    public string SharedByName { get; init; } = string.Empty;

    /// <summary>Display name of the recipient (for "Shared by me").</summary>
    public string SharedWithName { get; init; } = string.Empty;

    /// <summary>When the share was created.</summary>
    public DateTime SharedAt { get; init; }

    /// <summary>Expiration date (null = never).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Public link URL (only for PublicLink shares).</summary>
    public string? LinkUrl { get; init; }

    /// <summary>Download count (for public links).</summary>
    public int DownloadCount { get; init; }

    /// <summary>Max downloads (for public links, null = unlimited).</summary>
    public int? MaxDownloads { get; init; }

    /// <summary>Whether this share is expired.</summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

/// <summary>
/// Search result item for user/team/group search in the share dialog.
/// </summary>
public sealed class ShareSearchResult
{
    /// <summary>Entity ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Secondary text (e.g., email for users, member count for teams).</summary>
    public string? SecondaryText { get; init; }

    /// <summary>Result type: "User", "Team", or "Group".</summary>
    public string ResultType { get; init; } = "User";
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

    /// <summary>Storage backend type: "Local" or "S3".</summary>
    public string StorageBackend { get; set; } = "Local";

    /// <summary>S3-compatible storage endpoint URL (only when StorageBackend is "S3").</summary>
    public string S3Endpoint { get; set; } = string.Empty;

    /// <summary>S3 bucket name.</summary>
    public string S3BucketName { get; set; } = string.Empty;

    /// <summary>S3 access key ID.</summary>
    public string S3AccessKey { get; set; } = string.Empty;

    /// <summary>S3 secret access key.</summary>
    public string S3SecretKey { get; set; } = string.Empty;

    /// <summary>S3 region.</summary>
    public string S3Region { get; set; } = string.Empty;

    /// <summary>Whether Collabora Online integration is enabled.</summary>
    public bool CollaboraEnabled { get; set; }

    /// <summary>Collabora Online server URL (e.g. "https://collabora.example.com").</summary>
    public string CollaboraUrl { get; set; } = string.Empty;

    /// <summary>Whether to use the built-in Collabora CODE instance.</summary>
    public bool CollaboraUseBuiltIn { get; set; } = true;

    /// <summary>Auto-save interval in seconds for Collabora editing sessions.</summary>
    public int CollaboraAutoSaveIntervalSeconds { get; set; } = 300;

    /// <summary>Maximum concurrent Collabora editing sessions (0 = unlimited).</summary>
    public int CollaboraMaxSessions { get; set; }
}
