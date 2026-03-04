namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Status of a chunked upload session.
/// </summary>
public enum UploadSessionStatus
{
    /// <summary>Upload is actively receiving chunks.</summary>
    InProgress = 0,

    /// <summary>All chunks received and file assembly is complete.</summary>
    Completed = 1,

    /// <summary>Upload was cancelled by the user.</summary>
    Cancelled = 2,

    /// <summary>Upload session expired before completion.</summary>
    Expired = 3,

    /// <summary>Upload failed due to an error.</summary>
    Failed = 4
}
