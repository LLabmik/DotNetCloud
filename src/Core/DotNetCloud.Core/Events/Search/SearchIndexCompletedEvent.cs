namespace DotNetCloud.Core.Events.Search;

/// <summary>
/// Published by the Search module after an indexing batch completes.
/// Enables monitoring and progress tracking of reindex operations.
/// </summary>
public sealed record SearchIndexCompletedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The completion status of the indexing operation.</summary>
    public required IndexCompletionStatus Status { get; init; }

    /// <summary>The number of documents processed in this batch.</summary>
    public required int DocumentsProcessed { get; init; }

    /// <summary>The module that was indexed, if this was a module-specific operation. Null for global reindex.</summary>
    public string? ModuleId { get; init; }
}

/// <summary>
/// Completion status of a search indexing operation.
/// </summary>
public enum IndexCompletionStatus
{
    /// <summary>The indexing operation completed successfully.</summary>
    Success,

    /// <summary>The indexing operation completed with some errors.</summary>
    PartialSuccess,

    /// <summary>The indexing operation failed.</summary>
    Failed
}
