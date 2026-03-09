namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// A queued sync operation (upload, download, move, delete).
/// </summary>
public abstract class PendingOperationRecord
{
    /// <summary>Row ID (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Operation type discriminator.</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>UTC time the operation was queued.</summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Number of retry attempts so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>UTC time after which the operation is eligible for the next retry attempt. Null means immediately eligible.</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>Error message from the most recent failure. Null if the operation has not yet failed.</summary>
    public string? LastError { get; set; }
}

/// <summary>Pending file upload to the server.</summary>
public sealed class PendingUpload : PendingOperationRecord
{
    /// <summary>Full local file path to upload.</summary>
    public required string LocalPath { get; set; }

    /// <summary>Existing node ID (null for new files).</summary>
    public Guid? NodeId { get; set; }

    /// <summary>Initializes a new <see cref="PendingUpload"/>.</summary>
    public PendingUpload() { OperationType = "Upload"; }
}

/// <summary>Pending file download from the server.</summary>
public sealed class PendingDownload : PendingOperationRecord
{
    /// <summary>Server node ID to download.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Target local file path.</summary>
    public required string LocalPath { get; set; }

    /// <summary>POSIX mode from server. Applied after download on Linux.</summary>
    public int? PosixMode { get; set; }

    /// <summary>Initializes a new <see cref="PendingDownload"/>.</summary>
    public PendingDownload() { OperationType = "Download"; }
}
