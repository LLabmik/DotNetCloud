namespace DotNetCloud.Modules.Search.Data.Models;

/// <summary>
/// Tracks the progress and status of search reindex operations.
/// </summary>
public sealed class IndexingJob
{
    /// <summary>Unique identifier for this indexing job.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The module being indexed, or null for a global reindex.</summary>
    public string? ModuleId { get; set; }

    /// <summary>The type of indexing operation.</summary>
    public IndexJobType Type { get; set; }

    /// <summary>Current status of the job.</summary>
    public IndexJobStatus Status { get; set; }

    /// <summary>When the job started executing.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the job finished (successfully or with error).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Number of documents processed so far.</summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>Total number of documents to process (if known).</summary>
    public int DocumentsTotal { get; set; }

    /// <summary>Error message if the job failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// The type of indexing operation.
/// </summary>
public enum IndexJobType
{
    /// <summary>Full reindex of all documents in a module or globally.</summary>
    Full,

    /// <summary>Incremental index of changed documents only.</summary>
    Incremental
}

/// <summary>
/// Status of an indexing job.
/// </summary>
public enum IndexJobStatus
{
    /// <summary>Job is queued and waiting to execute.</summary>
    Pending,

    /// <summary>Job is currently executing.</summary>
    Running,

    /// <summary>Job completed successfully.</summary>
    Completed,

    /// <summary>Job failed with an error.</summary>
    Failed
}
