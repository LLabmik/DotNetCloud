namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Identifies the type of data being imported.
/// </summary>
public enum ImportDataType
{
    /// <summary>Contact records (vCard format).</summary>
    Contacts,

    /// <summary>Calendar events (iCalendar format).</summary>
    CalendarEvents,

    /// <summary>Notes (Markdown or plain text).</summary>
    Notes
}

/// <summary>
/// Source system from which data is being migrated.
/// </summary>
public enum ImportSource
{
    /// <summary>Generic / unknown source.</summary>
    Generic,

    /// <summary>Nextcloud instance.</summary>
    Nextcloud,

    /// <summary>Standard vCard/iCalendar file.</summary>
    StandardFile
}

/// <summary>
/// Outcome status for a single imported item.
/// </summary>
public enum ImportItemStatus
{
    /// <summary>Item was successfully imported (or would be in dry-run).</summary>
    Success,

    /// <summary>Item was skipped because a duplicate was detected.</summary>
    Skipped,

    /// <summary>Item failed validation or parsing.</summary>
    Failed,

    /// <summary>Item conflicted with an existing record.</summary>
    Conflict
}

/// <summary>
/// Request to execute a data import operation.
/// </summary>
public sealed record ImportRequest
{
    /// <summary>
    /// Type of data being imported.
    /// </summary>
    public required ImportDataType DataType { get; init; }

    /// <summary>
    /// Source system for migration metadata.
    /// </summary>
    public ImportSource Source { get; init; } = ImportSource.Generic;

    /// <summary>
    /// Raw import data (vCard text, iCalendar text, or note content).
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// When true, the import is validated and a report generated but no records are persisted.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Optional target container (e.g., calendar ID for events, folder ID for notes).
    /// </summary>
    public Guid? TargetContainerId { get; init; }

    /// <summary>
    /// Strategy for handling duplicate/conflicting records.
    /// </summary>
    public ImportConflictStrategy ConflictStrategy { get; init; } = ImportConflictStrategy.Skip;
}

/// <summary>
/// Strategy for handling records that conflict with existing data.
/// </summary>
public enum ImportConflictStrategy
{
    /// <summary>Skip conflicting records and continue.</summary>
    Skip,

    /// <summary>Overwrite existing records with imported data.</summary>
    Overwrite,

    /// <summary>Create a copy alongside the existing record.</summary>
    CreateCopy
}

/// <summary>
/// Result of a single imported item.
/// </summary>
public sealed record ImportItemResult
{
    /// <summary>
    /// Zero-based index of the item in the import data.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Display name or identifier for the item (e.g., contact name, event title).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Outcome status.
    /// </summary>
    public required ImportItemStatus Status { get; init; }

    /// <summary>
    /// ID of the created or matched record, if applicable.
    /// </summary>
    public Guid? RecordId { get; init; }

    /// <summary>
    /// Error or warning message, if any.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Complete report for an import operation.
/// </summary>
public sealed record ImportReport
{
    /// <summary>
    /// Whether this report is from a dry-run (no records persisted).
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// Type of data that was imported.
    /// </summary>
    public required ImportDataType DataType { get; init; }

    /// <summary>
    /// Source system identifier.
    /// </summary>
    public required ImportSource Source { get; init; }

    /// <summary>
    /// Total items found in the import data.
    /// </summary>
    public required int TotalItems { get; init; }

    /// <summary>
    /// Number of items successfully imported.
    /// </summary>
    public required int SuccessCount { get; init; }

    /// <summary>
    /// Number of items skipped (duplicates or conflicts).
    /// </summary>
    public required int SkippedCount { get; init; }

    /// <summary>
    /// Number of items that failed validation.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Number of items that conflicted with existing records.
    /// </summary>
    public required int ConflictCount { get; init; }

    /// <summary>
    /// Per-item results with details.
    /// </summary>
    public required IReadOnlyList<ImportItemResult> Items { get; init; }

    /// <summary>
    /// Conflict strategy that was applied.
    /// </summary>
    public required ImportConflictStrategy ConflictStrategy { get; init; }

    /// <summary>
    /// Timestamp when the import started.
    /// </summary>
    public required DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Timestamp when the import completed.
    /// </summary>
    public required DateTime CompletedAtUtc { get; init; }
}
