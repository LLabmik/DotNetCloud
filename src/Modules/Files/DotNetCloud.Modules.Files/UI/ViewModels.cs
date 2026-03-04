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
